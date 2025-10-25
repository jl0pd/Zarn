using System.Collections.Concurrent;
using System.Diagnostics;
using StreamRpc.Serialization;
using StreamRpc.TypeGeneration;
using StreamRpc.Utils;

namespace StreamRpc.Protocol;

internal sealed class ConnectionContext
{
    private readonly record struct OutputMessage(
                                        int Length,
                                        ChunkedArrayPoolBufferWriter<byte> Header,
                                        ChunkedArrayPoolBufferWriter<byte>? Body);

    private readonly Stream _stream;
    private readonly IServiceProvider _calleeServices;
    private readonly ConcurrentQueue<OutputMessage> _outputMessages = new();
    private readonly AsyncAutoResetEvent _outputMessagesEvent = new();
    private readonly ConcurrentDictionary<ObjectId, InvokerState> _invokers = new();
    private readonly CalleesState _callees;
    private readonly SemaphoreSlim _concurrentOperationsSemaphore;
    private readonly int _maxConcurrentOperations;
    private readonly ConcurrentDictionary<ObjectId, (CalleeFactory Factory, object Instance)> _trackedObjects = new();

    public Pools Pools { get; }

    public RpcSettings Settings { get; }

    public BinarySerializationContext SerializationContext { get; }

    public ConnectionContext(Stream stream, Pools pools, RpcSettings settings, IServiceProvider services)
    {
        _stream = stream;
        _calleeServices = services;
        Pools = pools;
        Settings = settings;
        SerializationContext = pools.SerializationContext; // keep it closer
        _maxConcurrentOperations = settings.MaxConcurrentOperations;
        _callees = new CalleesState(this, _maxConcurrentOperations);
        _concurrentOperationsSemaphore = new(_maxConcurrentOperations, _maxConcurrentOperations);
    }

    public InvokerBase GetInvoker(Type invokerType)
    {
        int typeSlot = -1;
        Type interfaceType = invokerType.IsConstructedGenericType
                                ? invokerType.GetGenericTypeDefinition()
                                : invokerType;

        var factories = Pools.InvokerFactories;
        for (int i = 0; i < factories.Length; i++)
        {
            if (factories[i].InterfaceType == interfaceType)
            {
                typeSlot = i;
                break;
            }
        }

        if (typeSlot < 0)
        {
            throw new KeyNotFoundException("Unable to find implementation for type" + invokerType);
        }

        Type[] genericArgs = [];
        InvokerBase invoker;
        if (invokerType.IsConstructedGenericType)
        {
            genericArgs = invokerType.GetGenericArguments();
            invoker = factories[typeSlot].GetInvoker(genericArgs);
        }
        else
        {
            invoker = factories[typeSlot].GetInvoker();
        }
        
        invoker.State = new InvokerState(this,
                                         typeSlot + 1,
                                         genericArgs,
                                         _maxConcurrentOperations,
                                         _concurrentOperationsSemaphore);
        
        _invokers.AddOrUpdate(invoker.State.Id, invoker.State, (key, value) =>
        {
            const string message = "Multiple invokers with same id may not exist";
            Debug.Fail(message);
            throw new InvalidOperationException(message);
        });

        return invoker;
    }

    public async Task SendMessages(CancellationToken cancellationToken)
    {
        Debug.Assert(cancellationToken.CanBeCanceled);

        using var _ = cancellationToken.UnsafeRegister(
                        static state => ((AsyncAutoResetEvent?)state)?.Set(),
                        _outputMessagesEvent);

        while (!cancellationToken.IsCancellationRequested)
        {
            await _outputMessagesEvent.WaitAsync();

            while (_outputMessages.TryDequeue(out var msg))
            {
                int chunkId = 0;
                foreach (var chunk in msg.Header)
                {
                    ReadOnlyMemory<byte> memoryToWrite;
                    if (chunkId == 0)
                    {
                        int msgBytesLength = PackedInt.GetRequiredSize(msg.Length - PackedInt.MaxSize);
                        int skipBytes = PackedInt.MaxSize - msgBytesLength;
                        int written = PackedInt.Write(msg.Length - skipBytes, chunk.Array.AsSpan(skipBytes));
                        Debug.Assert(written == msgBytesLength);
                        memoryToWrite = chunk.Array.AsMemory(skipBytes, chunk.Written - skipBytes);
                    }
                    else
                    {
                        memoryToWrite = chunk.WrittenMemory;
                    }

                    await _stream.WriteAsync(memoryToWrite, cancellationToken);

                    chunkId++;
                }

                if (msg.Body is { })
                {
                    foreach (var chunk in msg.Body)
                    {
                        await _stream.WriteAsync(chunk.WrittenMemory, cancellationToken);
                    }
                }

                Pools.Return(msg.Header);
                Pools.Return(msg.Body);
            }
        }
    }

    public async Task ReadMessages(CancellationToken cancellationToken)
    {
        var initialBuffer = new byte[PackedInt.MaxSize];
        while (!cancellationToken.IsCancellationRequested)
        {
            var writer = Pools.GetWriter();

            int bytesRead = await _stream.ReadAsync(initialBuffer.AsMemory(0, 1), cancellationToken);
            if (bytesRead == 0)
            {
                Pools.Return(writer);
                return;
            }

            var headerLength = PackedInt.GetConsumedBytes(initialBuffer[0]);
            if (headerLength > 1)
            {
                while (bytesRead < headerLength)
                {
                    var read = await _stream.ReadAsync(initialBuffer.AsMemory(bytesRead, headerLength - bytesRead), cancellationToken);
                    if (read == 0)
                    {
                        Pools.Return(writer);
                        return;
                    }
                    bytesRead += read;
                }
            }

            var messageLength = (int)PackedInt.Read(initialBuffer, out _);

            var payloadLength = messageLength - headerLength;
            var message = writer.GetMemory(payloadLength);
            bytesRead = 0;
            while (bytesRead < payloadLength)
            {
                var read = await _stream.ReadAsync(message[bytesRead..payloadLength], cancellationToken);
                if (read == 0)
                {
                    Pools.Return(writer);
                    return;
                }

                bytesRead += read;
            }
            writer.Advance(payloadLength);

            HandleMessage(writer);
        }
    }

    private void HandleMessage(ChunkedArrayPoolBufferWriter<byte> message)
    {
        var type = (MessageType)message.FirstChunkRequired.Array[0];
        switch (type)
        {
            case MessageType.Error:
                throw ThrowHelper.Fail("Error message received");

            case MessageType.ExecuteRequest:
                HandleExecuteRequest(message);
                break;
            case MessageType.ExecuteResponse:
                HandleExecuteResponse(message);
                break;
            case MessageType.ExecuteCancel:
                HandleExecuteCancel(message);
                break;
            case MessageType.GetRemoteIdRequest:
                HandleGetRemoteIdRequest(message);
                break;
            case MessageType.GetRemoteIdResponse:
                HandleGetRemoteIdResponse(message);
                break;
            default:
                Debug.Fail("Invalid message");
                break;
        }
    }

    private void HandleGetRemoteIdResponse(ChunkedArrayPoolBufferWriter<byte> message)
    {
        var reader = message.GetReader();
        reader.Advance(1);

        var invokerId = SerializationContext.Deserialize<ObjectId>(ref reader);
        var remoteId = SerializationContext.Deserialize<ObjectId>(ref reader);

        _invokers[invokerId].SetRemoteId(remoteId);

        Pools.Return(message);
    }

    private void HandleGetRemoteIdRequest(ChunkedArrayPoolBufferWriter<byte> message)
    {
        var reader = message.GetReader();
        reader.Advance(1);

        var invokerId = SerializationContext.Deserialize<ObjectId>(ref reader);
        var typeSlot = SerializationContext.Deserialize<int>(ref reader) - 1;
        var genericArgs = SerializationContext.Deserialize<Type[]>(ref reader);

        var factory = Pools.CalleeFactories[typeSlot];
        var calleeType = genericArgs.Length > 0
            ? factory.ImplementationType.MakeGenericType(genericArgs)
            : factory.InterfaceType;

        var serviceInstance = _calleeServices.GetService(calleeType)
            ?? throw ThrowHelper.Unreachable; // service must be registered

        var remoteId = ObjectId.GenObjectId();
        _trackedObjects.AddOrUpdate(remoteId, (factory, serviceInstance), (k, v) =>
        {
            throw ThrowHelper.Unreachable;
        });

        message.Reset();
        message.Reserve(PackedInt.MaxSize);
        SerializationContext.Serialize(MessageType.GetRemoteIdResponse, message);
        SerializationContext.Serialize(invokerId, message);
        SerializationContext.Serialize(remoteId, message);
        Dispatch(message);
    }

    private void HandleExecuteCancel(ChunkedArrayPoolBufferWriter<byte> message)
    {
        var reader = message.GetReader();
        reader.Advance(1);

        var opId = SerializationContext.Deserialize<OperationId>(ref reader);

        // it's possible for cancellation to arrive after operation is already completed
        if (_callees.Find(opId) is { } op)
        {
            Interlocked.Exchange(ref op.Cts, null)?.Cancel();
        }
    }

    private void HandleExecuteRequest(ChunkedArrayPoolBufferWriter<byte> message)
    {
        var reader = message.GetReader();
        var options = (ExecuteRequestOptions)reader.UnreadSpan[1];

        reader.Advance(2);

        var operationId = SerializationContext.Deserialize<OperationId>(ref reader);
        var remoteId = SerializationContext.Deserialize<ObjectId>(ref reader);

        var (factory, instance) = _trackedObjects[remoteId];

        CalleeBase callee = factory.GetCallee();
        callee.MethodSlot = SerializationContext.Deserialize<int>(ref reader);
        if (options.HasFlag(ExecuteRequestOptions.GenericMethod))
        {
            callee.GenericMethodArgs = SerializationContext.Deserialize<Type[]>(ref reader);
        }

        callee.Factory = factory;
        callee.Callees = _callees;
        callee.OperationId = operationId;
        if (!_callees.Register(callee))
        {
            // TODO: handle case when caller does more than `MaxConcurrentOperations` calls at same time.
            // Currently this doesn't happen, and unlikely to happen in future, at least while people won't start
            // implementing own libs to talk to this
            Debug.Fail("not implemented");
            throw new NotImplementedException();
        }
        callee.Arguments = message;
        callee.Impl = instance;
        callee.ReaderOffset = reader.Consumed;
        callee.Cts = Pools.GetCts();

        ThreadPool.UnsafeQueueUserWorkItem(callee, false);
    }

    private void HandleExecuteResponse(ChunkedArrayPoolBufferWriter<byte> message)
    {
        var reader = message.GetReader();
        reader.Advance(2);

        var opId = SerializationContext.Deserialize<OperationId>(ref reader);

        var invoker = _invokers[opId.Target];

        var options = (ExecuteResponseOptions)message.FirstChunkRequired.Array[1];
        if (options.HasFlag(ExecuteResponseOptions.Success))
        {
            invoker.Complete(opId.Id, ref reader);
        }
        else
        {
            var ex = (Exception?)SerializationContext.DeserializeAny(ref reader);
            Debug.Assert(ex is { });
            invoker.Complete(opId.Id, ex);
        }

        Debug.Assert(reader.Remaining == 0);

        Pools.Return(message);
    }

    public void Dispatch(ChunkedArrayPoolBufferWriter<byte> header)
    {
        Dispatch(header, null);
    }

    public void Dispatch(ChunkedArrayPoolBufferWriter<byte> header, ChunkedArrayPoolBufferWriter<byte>? body)
    {
        long length = header.TotalLength;
        if (body is { })
        {
            length += body.TotalLength;
        }

        _outputMessages.Enqueue(new OutputMessage(checked((int)length), header, body));
        _outputMessagesEvent.Set();
    }
}
