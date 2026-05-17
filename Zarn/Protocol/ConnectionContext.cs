using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Zarn.Collections;
using Zarn.EnumerableSupport;
using Zarn.Invocation;
using Zarn.Protocol.Messages;
using Zarn.Serialization;
using Zarn.Utils;

namespace Zarn.Protocol;

internal sealed class ConnectionContext : IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly ConcurrentQueue<ChunkedArrayPoolBufferWriter<byte>> _outputMessages = new();
    private readonly AsyncAutoResetEvent _outputMessagesEvent = new();
    private ExceptionDispatchInfo? _failure;
    private long _nextObjectId = 1;

    public int MaxConcurrentOperations { get; }

    public CalleeOperations CalleeOperations { get; }

    public SemaphoreSlim ConcurrentOperationsSemaphore { get; }

    public Pools Pools { get; }

    public RpcSettings Settings { get; }

    public BinarySerializationContext SerializationContext { get; }

    public InstanceManager InstanceManager { get; }

    public bool IsServer { get; }

    public ConnectionContext(bool isServer, Stream stream, Pools pools, RpcSettings settings, IServiceProvider services)
    {
        IsServer = isServer;
        _stream = stream;
        Pools = pools;
        Settings = settings;
        SerializationContext = pools.SerializationContext; // keep it closer
        SerializationContext.SetConnection(this);
        MaxConcurrentOperations = settings.MaxConcurrentOperations;
        CalleeOperations = new CalleeOperations(MaxConcurrentOperations);
        ConcurrentOperationsSemaphore = new(MaxConcurrentOperations, MaxConcurrentOperations);
        InstanceManager = new InstanceManager(this, services);
    }

    public ObjectId GenObjectId() => new(Interlocked.Increment(ref _nextObjectId), IsServer);

    public ValueTask DisposeAsync()
    {
        ConcurrentOperationsSemaphore.Dispose();
        return _stream.DisposeAsync();
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
            _failure?.Throw();

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            // TODO: not sure whether it's perfect magic number
            const int magicThreshold = 65536;

            var rented = ArrayPool<byte>.Shared.Rent(magicThreshold);
            int buffered = 0;
            long unflushed = 0;

            while (_outputMessages.TryDequeue(out var msg))
            {
                // Do not buffer single message that was written in one chunk because it will incur useless copy.
                // Unfortunately this isn't perfect check, it's still possible to create useless copies
                // if small and big chunks are placed one after another in following manner: .-.-.-.-.
                bool shouldBuffer = !_outputMessages.IsEmpty || !msg.IsSingleChunk;

                foreach (var chunk in msg)
                {
                    var memoryToWrite = chunk.ChunkIndex == 0
                                        ? StreamHelper.PrepareFirstChunk(msg.TotalLength, chunk)
                                        : chunk.WrittenMemory;

                    // if chunk can fit into write-buffer, then buffer it
                    int memoryLength = memoryToWrite.Length;
                    if (shouldBuffer && memoryLength <= rented.Length - buffered)
                    {
                        memoryToWrite.Span.CopyTo(rented.AsSpan(buffered));
                        buffered += memoryLength;
                    }
                    else
                    {
                        // otherwise send buffered data
                        if (buffered > 0)
                        {
                            await _stream.WriteAsync(rented.AsMemory(0, buffered), cancellationToken);
                            unflushed += buffered;
                            buffered = 0;
                        }

                        // Write-buffer overflow may be caused by small chunk.
                        // If this chunk is actually small then buffer it, otherwise send it right away
                        if (shouldBuffer && memoryLength <= rented.Length)
                        {
                            memoryToWrite.Span.CopyTo(rented.AsSpan(buffered));
                            buffered += memoryLength;
                        }
                        else
                        {
                            await _stream.WriteAsync(memoryToWrite, cancellationToken);
                            unflushed += memoryLength;
                        }

                        if (unflushed > magicThreshold)
                        {
                            await _stream.FlushAsync(cancellationToken);
                            unflushed = 0;
                        }
                    }
                }

                Pools.Return(msg);
            }

            if (buffered > 0)
            {
                await _stream.WriteAsync(rented.AsMemory(0, buffered), cancellationToken);
                unflushed += buffered;
            }

            ArrayPool<byte>.Shared.Return(rented);

            if (unflushed > 0)
            {
                await _stream.FlushAsync(cancellationToken);
                unflushed = 0;
            }
        }
    }

    public async Task ReadMessages(CancellationToken cancellationToken)
    {
        var initialBuffer = new byte[PackedInt.MaxSize];
        while (!cancellationToken.IsCancellationRequested)
        {
            var writer = Pools.GetWriter();

            int bytesRead;
            try
            {
                bytesRead = await _stream.ReadAsync(initialBuffer.AsMemory(0, 1), cancellationToken);
            }
            catch
            {
                // ignore exception that may be raised when reading from stream
                // because there's no way to stop pending read in non-destructive way
                bytesRead = 0;
            }
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
                        ThrowHelper.ThrowEndOfStream();
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
                    ThrowHelper.ThrowEndOfStream();
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
                throw new InvalidOperationException("Error message received");

            case MessageType.ExecuteRequest:
                HandleExecuteRequest(message);
                break;
            case MessageType.ExecuteResponse:
                HandleExecuteResponse(message);
                break;
            case MessageType.ExecuteCancelNotification:
                HandleExecuteCancel(message);
                break;
            case MessageType.CreateInstanceRequest:
                HandleCreateInstanceRequest(message);
                break;
            case MessageType.CreateInstanceResponse:
                HandleCreateInstanceResponse(message);
                break;
            case MessageType.ObjectCollectedNotification:
                HandleObjectCollectedNotification(message);
                break;
            case MessageType.GetEnumeratorRequest:
                HandleGetEnumeratorRequest(message);
                break;
            case MessageType.CancelAsyncEnumeratorNotification:
                HandleCancelAsyncEnumeratorNotification(message);
                break;
            default:
                throw new InvalidDataException("Invalid message was received:" + type);
        }
    }

    private void HandleObjectCollectedNotification(ChunkedArrayPoolBufferWriter<byte> message)
    {
        var reader = message.GetReader();
        reader.Advance(1);

        var notification = ObjectCollectedNotification.Deserialize(ref reader, SerializationContext);
        InstanceManager.RemoveObject(notification.InstanceId);
    }

    private void HandleGetEnumeratorRequest(ChunkedArrayPoolBufferWriter<byte> message)
    {
        var reader = message.GetReader();
        reader.Advance(1);

        var request = GetEnumeratorMessageRequest.Deserialize(ref reader, SerializationContext);

        var dispatcher = Pools.GetGetEnumeratorDispatcher();
        dispatcher.Connection = this;
        dispatcher.InvokerId = request.InvokerId;
        dispatcher.TypeArg = request.TypeArg;
        dispatcher.IsAsync = request.IsAsync;
        dispatcher.EnumerableId = request.EnumerableId;

        InvokeDispatcher(dispatcher);
    }

    private void HandleCreateInstanceRequest(ChunkedArrayPoolBufferWriter<byte> message)
    {
        var reader = message.GetReader();
        reader.Advance(1);

        var request = CreateInstanceMessageRequest.Deserialize(ref reader, SerializationContext);

        var dispatcher = Pools.GetCreateInstanceDispatcher();
        dispatcher.Connection = this;
        dispatcher.InvokerId = request.InvokerId;
        dispatcher.TypeSlot = request.TypeSlot;
        dispatcher.GenericArgs = request.GenericArgs;

        InvokeDispatcher(dispatcher);
    }

    private void HandleCancelAsyncEnumeratorNotification(ChunkedArrayPoolBufferWriter<byte> message)
    {
        var reader = message.GetReader();
        reader.Advance(1);

        var response = CancelAsyncEnumeratorNotification.Deserialize(ref reader, SerializationContext);
        var cts = InstanceManager.GetDescriptor(response.EnumeratorId).Cts;

        Debug.Assert(cts is { });
        InvokeCancellation(cts);
    }

    private void HandleCreateInstanceResponse(ChunkedArrayPoolBufferWriter<byte> message)
    {
        var reader = message.GetReader();
        reader.Advance(1);

        var response = CreateInstanceMessageResponse.Deserialize(ref reader, SerializationContext);
        var invoker = InstanceManager.GetInvokerState(response.InvokerId);
        invoker.SetRemoteId(in response);
    }

    private void HandleExecuteCancel(ChunkedArrayPoolBufferWriter<byte> message)
    {
        var reader = message.GetReader();
        reader.Advance(1);

        var notification = ExecuteCancelNotification.Deserialize(ref reader, SerializationContext);

        // it's possible for cancellation to arrive after operation is already completed
        if (CalleeOperations.Find(notification.OperationId) is { } op
        && Interlocked.Exchange(ref op.Cts, null) is { } cts)
        {
            InvokeCancellation(cts);
        }
    }

    private void InvokeCancellation(CancellationTokenSource cts)
    {
        var dispatcher = Pools.GetCtsCancelDispatcher();
        dispatcher.Connection = this;
        dispatcher.CancellationTokenSource = cts;

        InvokeDispatcher(dispatcher);
    }

    private void HandleExecuteRequest(ChunkedArrayPoolBufferWriter<byte> messageBuffer)
    {
        var dispatcher = Pools.GetExecuteRequestDispatcher();
        dispatcher.MessageBuffer = messageBuffer;
        dispatcher.Connection = this;

        InvokeDispatcher(dispatcher);
    }

    private void HandleExecuteResponse(ChunkedArrayPoolBufferWriter<byte> messageBuffer)
    {
        var dispatcher = Pools.GetExecuteResponseDispatcher();
        dispatcher.MessageBuffer = messageBuffer;
        dispatcher.Connection = this;

        InvokeDispatcher(dispatcher);
    }

    private static void InvokeDispatcher(IThreadPoolWorkItem dispatcher)
    {
        ThreadPool.UnsafeQueueUserWorkItem(dispatcher, false);
    }

    public void Dispatch(ChunkedArrayPoolBufferWriter<byte> message)
    {
        _outputMessages.Enqueue(message);
        _outputMessagesEvent.Set();
    }

    public void Dispatch<T>(T message) where T : struct, IMessageInternal<T>
    {
        var writer = Pools.GetWriter();
        writer.Reserve(PackedInt.MaxSize);

        var span = writer.GetSpan();
        span[0] = (byte)message.Type;
        writer.Advance(1);

        message.Serialize(writer, SerializationContext);

        Dispatch(writer);
    }

    public void Fail(Exception exception)
    {
        Interlocked.CompareExchange(ref _failure, ExceptionDispatchInfo.Capture(exception), null);
        _outputMessagesEvent.Set();
    }
}
