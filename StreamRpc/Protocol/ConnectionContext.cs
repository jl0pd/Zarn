using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using StreamRpc.Serialization;
using StreamRpc.Utils;

namespace StreamRpc.Protocol;

internal sealed class ConnectionContext : IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly ConcurrentQueue<ChunkedArrayPoolBufferWriter<byte>> _outputMessages = new();
    private readonly AsyncAutoResetEvent _outputMessagesEvent = new();

    public int MaxConcurrentOperations { get; }

    public CalleeOperations CalleeOperations { get; }

    public SemaphoreSlim ConcurrentOperationsSemaphore { get; }

    public Pools Pools { get; }

    public RpcSettings Settings { get; }

    public BinarySerializationContext SerializationContext { get; }

    public InstanceManager InstanceManager { get; }

    public IInstanceManager RemoteInstanceManager { get; }

    public ConnectionContext(Stream stream, Pools pools, RpcSettings settings, IServiceProvider services)
    {
        _stream = stream;
        Pools = pools;
        Settings = settings;
        SerializationContext = pools.SerializationContext; // keep it closer
        SerializationContext.SetConnection(this);
        MaxConcurrentOperations = settings.MaxConcurrentOperations;
        CalleeOperations = new CalleeOperations(MaxConcurrentOperations);
        ConcurrentOperationsSemaphore = new(MaxConcurrentOperations, MaxConcurrentOperations);
        InstanceManager = new InstanceManager(this, services);
        RemoteInstanceManager = GetCommunicationService<IInstanceManager>(CommunicationServices.InstanceManager);
    }

    private T GetCommunicationService<T>(ObjectId id) where T : class
    {
        var invoker = InstanceManager.GetInvoker(typeof(T), id, false);
        invoker.State.SetRemoteId(id);
        return (T)(object)invoker;
    }

    public ValueTask DisposeAsync()
    {
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
            default:
                Debug.Fail("Invalid message");
                break;
        }
    }

    private void HandleExecuteCancel(ChunkedArrayPoolBufferWriter<byte> message)
    {
        var reader = message.GetReader();
        reader.Advance(1);

        var opId = SerializationContext.Deserialize<OperationId>(ref reader);

        // it's possible for cancellation to arrive after operation is already completed
        if (CalleeOperations.Find(opId) is { } op)
        {
            Interlocked.Exchange(ref op.Cts, null)?.Cancel();
        }
    }

    private void HandleExecuteRequest(ChunkedArrayPoolBufferWriter<byte> messageBuffer)
    {
        Unsafe.SkipInit(out ExecuteRequestMessage message);

        message.Deserialize(messageBuffer, SerializationContext);

        var descriptor = InstanceManager.GetDescriptor(message.RemoteId);

        CalleeBase callee = descriptor.CalleeFactory.Get();
        callee.MethodSlot = message.MethodSlot;
        callee.GenericMethodArgs = message.GenericMethodArgs;
        callee.Factory = descriptor.CalleeFactory;
        callee.Connection = this;
        callee.OperationId = message.OperationId;
        callee.Arguments = messageBuffer;
        callee.Impl = descriptor.Instance;
        callee.ReaderOffset = message.ReaderOffset;
        callee.Cts = Pools.GetCts();

        if (!CalleeOperations.Register(callee))
        {
            // TODO: handle case when caller does more than `MaxConcurrentOperations` calls at same time.
            // Currently this doesn't happen, and unlikely to happen in future, at least while people won't start
            // implementing own libs to talk to this
            Debug.Fail("not implemented");
            throw new NotImplementedException();
        }

        ThreadPool.UnsafeQueueUserWorkItem(callee, false);
    }

    private void HandleExecuteResponse(ChunkedArrayPoolBufferWriter<byte> message)
    {
        var reader = message.GetReader();
        reader.Advance(2);

        var opId = SerializationContext.Deserialize<OperationId>(ref reader);

        var invoker = InstanceManager.GetInvokerState(opId.Target);

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

    public void Dispatch(ChunkedArrayPoolBufferWriter<byte> message)
    {
        _outputMessages.Enqueue(message);
        _outputMessagesEvent.Set();
    }
}
