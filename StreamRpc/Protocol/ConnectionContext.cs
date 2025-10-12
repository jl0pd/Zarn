using System.Collections.Concurrent;
using System.Diagnostics;
using StreamRpc.Serialization;
using StreamRpc.Utils;

namespace StreamRpc.Protocol;

internal sealed class ConnectionContext
{
    private readonly record struct OutputMessage(
                                        MessageOptions Options,
                                        int Length,
                                        ChunkedArrayPoolBufferWriter<byte> Header,
                                        ChunkedArrayPoolBufferWriter<byte>? Body);

    private readonly Stream _stream;
    private readonly MessageOptions _commonOptions;
    private readonly IServiceProvider _calleeServices;
    private readonly ConcurrentDictionary<OperationId, InvokerOperation> _invokerOperations = new();
    private readonly ConcurrentDictionary<OperationId, CalleeBase> _calleeOperations = new();
    private readonly ConcurrentQueue<OutputMessage> _outputMessages = new();
    private readonly AsyncManualResetEvent _outputMessagesEvent = new();

    public Pools Pools { get; }

    public BinarySerializationContext SerializationContext { get; }

    public ConnectionContext(Stream stream, Pools pools, IServiceProvider services)
    {
        _stream = stream;
        _calleeServices = services;
        Pools = pools;
        SerializationContext = pools.SerializationContext; // keep it closer
    }

    public InvokerBase GetInvoker(Type InvokerType)
    {
        int typeSlot = -1;
        Type interfaceType = InvokerType.IsConstructedGenericType
                                ? InvokerType.GetGenericTypeDefinition()
                                : InvokerType;

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
            throw new KeyNotFoundException("Unable to find implementation for type" + InvokerType);
        }

        InvokerBase Invoker;
        if (InvokerType.IsConstructedGenericType)
        {
            Invoker = factories[typeSlot].GetInvoker(InvokerType.GetGenericArguments());
        }
        else
        {
            Invoker = factories[typeSlot].GetInvoker();
        }
        Invoker.Connection = this;
        Invoker.TypeSlot = typeSlot + 1;
        Invoker.Id = OperationId.GenObjectId();

        return Invoker;
    }

    public async Task SendMessages(CancellationToken cancellationToken)
    {
        Debug.Assert(cancellationToken.CanBeCanceled);

        using var _ = cancellationToken.UnsafeRegister(
                        static state => ((AsyncManualResetEvent?)state)?.Set(),
                        _outputMessagesEvent);

        while (!cancellationToken.IsCancellationRequested)
        {
            await _outputMessagesEvent.WaitAsync();
            _outputMessagesEvent.Reset();

            while (_outputMessages.TryDequeue(out var msg))
            {
                if (_commonOptions.HasFlag(MessageOptions.Compressed))
                {
                    throw new NotImplementedException();
                }

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

                        chunk.Array[PackedInt.MaxSize] = (byte)(msg.Options | _commonOptions | MessageOptions.LastChunk);
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
        Debug.Assert(((MessageOptions)message.FirstChunkRequired.Array[0] & MessageOptions.ReservedMask) == 0);
        var type = (MessageType)message.FirstChunkRequired.Array[1];
        switch (type)
        {
            case MessageType.ExecuteRequest:
                HandleExecuteRequest(message);
                break;
            case MessageType.ExecuteResponse:
                HandleExecuteResponse(message);
                break;
            case MessageType.ExecuteCancel:
                HandleExecuteCancel(message);
                break;
            default:
                Debug.Fail("Invalid message");
                break;
        }
    }

    private void HandleExecuteCancel(ChunkedArrayPoolBufferWriter<byte> message)
    {
        var reader = message.GetReader();
        reader.Advance(2);

        var opId = SerializationContext.Deserialize<OperationId>(ref reader);

        // it's possible for cancellation to arrive after operation is already completed
        if (_calleeOperations.TryGetValue(opId, out var op))
        {
            Interlocked.Exchange(ref op.Cts, null)?.Cancel();
        }
    }

    private void HandleExecuteRequest(ChunkedArrayPoolBufferWriter<byte> message)
    {
        var reader = message.GetReader();
        var options = (MessageOptions)reader.FirstSpan[0];

        reader.Advance(2);

        var operationId = SerializationContext.Deserialize<OperationId>(ref reader);
        var typeSlot = SerializationContext.Deserialize<int>(ref reader) - 1;
        Type calleeType;
        CalleeBase callee;
        if (options.HasFlag(MessageOptions.GenericType))
        {
            int argsCount = SerializationContext.Deserialize<int>(ref reader);
            var typeArgs = new Type[argsCount];
            for (int i = 0; i < typeArgs.Length; i++)
            {
                typeArgs[i] = SerializationContext.Deserialize<Type>(ref reader);
            }

            calleeType = Pools.CalleeFactories[typeSlot].ImplementationType.MakeGenericType(typeArgs);
            callee = (CalleeBase)Activator.CreateInstance(calleeType)!;
        }
        else
        {
            calleeType = Pools.CalleeFactories[typeSlot].InterfaceType;
            callee = Pools.CalleeFactories[typeSlot].GetCallee();
        }

        callee.MethodSlot = SerializationContext.Deserialize<int>(ref reader);
        if (options.HasFlag(MessageOptions.GenericMethod))
        {
            int argsCount = SerializationContext.Deserialize<int>(ref reader);
            var typeArgs = new Type[argsCount];
            for (int i = 0; i < typeArgs.Length; i++)
            {
                typeArgs[i] = SerializationContext.Deserialize<Type>(ref reader);
            }

            callee.GenericMethodArgs = typeArgs;
        }

        callee.Connection = this;
        callee.OperationId = operationId;
        callee.Arguments = message;
        callee.Impl = _calleeServices.GetService(calleeType) ?? throw new InvalidOperationException();
        callee.ArgumentsReader = reader;
        callee.Cts = Pools.GetCts();

        ThreadPool.UnsafeQueueUserWorkItem(callee, false);
    }

    private void HandleExecuteResponse(ChunkedArrayPoolBufferWriter<byte> message)
    {
        var reader = message.GetReader();
        reader.Advance(2);

        var opId = SerializationContext.Deserialize<OperationId>(ref reader);

        var op = _invokerOperations.Remove(opId);

        var options = (MessageOptions)message.FirstChunkRequired.Array[0];
        if (options.HasFlag(MessageOptions.Success))
        {
            op.Complete(SerializationContext, ref reader);
        }
        else
        {
            var ex = SerializationContext.Deserialize<Exception>(ref reader);
            op.Complete(ex);
        }

        Pools.Return(message);
    }

    public InvokerOperation<T> StartInvokerOperation<T>(OperationId operationId)
    {
        var operation = Pools.GetInvokerOperation<T>();
        operation.Id = operationId;
        RegisterInvokerOperation(operation, operationId);
        return operation;
    }

    public VoidInvokerOperation StartVoidInvokerOperation(OperationId operationId)
    {
        var operation = Pools.GetInvokerOperation();
        RegisterInvokerOperation(operation, operationId);
        return operation;
    }

    private void RegisterInvokerOperation(InvokerOperation operation, OperationId operationId)
    {
        operation.Context = this;
        operation.Id = operationId;
        _invokerOperations.AddOrUpdate(operationId, operation, (k, v) =>
        {
            string message = "Operation with same Id cannot be registered";
            Debug.Fail(message);
            throw new InvalidOperationException(message);
        });
    }

    public void Dispatch(MessageOptions options, ChunkedArrayPoolBufferWriter<byte> header, ChunkedArrayPoolBufferWriter<byte>? body)
    {
        long length = header.TotalLength;
        if (body is { })
        {
            length += body.TotalLength;
        }

        _outputMessages.Enqueue(new OutputMessage(options, checked((int)length), header, body));
        _outputMessagesEvent.Set();
    }

    public void CompleteResponse(CalleeBase callee, Exception? exception, ChunkedArrayPoolBufferWriter<byte>? returnValue)
    {
        var options = exception is null ? MessageOptions.Success : MessageOptions.None;
        var header = Pools.GetWriter();

        header.Reserve(PackedInt.MaxSize);
        SerializationContext.Serialize(options, header);
        SerializationContext.Serialize(MessageType.ExecuteResponse, header);
        SerializationContext.Serialize(callee.OperationId, header);
        if (exception is not null)
        {
            SerializationContext.Serialize(exception, header);
        }

        Dispatch(options, header, returnValue);

        Pools.Return(Interlocked.Exchange(ref callee.Cts, null));

        var @interface = callee.ImplementedInterface;
        if (!@interface.IsGenericType)
        {
            Pools.CalleeFactoryLookup[@interface].Return(callee);
        }
    }
}
