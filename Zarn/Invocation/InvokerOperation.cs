using System.Buffers;
using System.Diagnostics;
using Zarn.Collections;
using Zarn.Protocol;
using Zarn.Protocol.Messages;
using Zarn.Serialization;

namespace Zarn.Invocation;

internal abstract class InvokerOperation
{
    // this token is not associate in any way with `IValueTaskCompletionSource.GetResult(short token)`
    public short Token { get; set; }

    public InvokerState? Invoker { get; set; }

    public ConnectionContext? Connection => Invoker?.Connection;

    protected BinarySerializationContext? SerializationContext { get; private set; }

    public CancellationToken CancellationToken { get; set; }

    private CancellationTokenRegistration _cancellationTokenRegistration;

    public ChunkedArrayPoolBufferWriter<byte>? RequestWriter { get; set; }

    public ExecuteRequestOptions RequestOptions
    {
        get => _message.Options;
        set => _message.Options = value;
    }

    public int MethodSlot
    {
        get => _message.MethodSlot;
        set => _message.MethodSlot = value;
    }

    private int _isResultSet = 0;

    private Task? _waitForFreeOperationSlot;
    private Action? _startCoreAction;
    private Action? _onRemoteIdReadyAction;

    private ExecuteRequestMessage _message;

    public Pools? Reset()
    {
        _cancellationTokenRegistration.Dispose();
        _cancellationTokenRegistration = default;
        _isResultSet = 0;
        RequestOptions = ExecuteRequestOptions.None;
        var pools = Invoker?.Connection.Pools;
        Invoker = null;
        return pools;
    }

    public void Prepare()
    {
        Debug.Assert(Connection is { });
        SerializationContext = Connection.SerializationContext;
        var writer = Connection.Pools.GetWriter();
        writer.Reserve(PackedInt.MaxSize + ExecuteRequestMessage.MaxHeaderSize);
        if (Connection.Pools.CompressionProvider is { })
        {
            RequestOptions |= ExecuteRequestOptions.Compressed;
        }
        RequestWriter = writer;
    }

    public void SerializeArg<T>(T value)
    {
        Debug.Assert(SerializationContext is { } && RequestWriter is { });

        SerializationContext.Serialize(value, RequestWriter);
    }

    protected void StartCommon()
    {
        StartCommonCore(true);
    }

    private void StartCommonWhenCompleted()
    {
        StartCommonCore(false);
    }

    private void StartCommonCore(bool isSynchronous)
    {
        try
        {
            CancellationToken.ThrowIfCancellationRequested();
            Debug.Assert(RequestWriter is { } && Connection is { } && Invoker is { });

            var ridTask = Invoker.RemoteId;
            if (ridTask.IsCompleted)
            {
                ridTask.GetAwaiter().GetResult();

                var waitTask = Invoker.WaitForFreeOperationSlot(CancellationToken);
                if (waitTask.IsCompleted)
                {
                    waitTask.GetAwaiter().GetResult(); // throw exception is there's any
                    StartCore();
                }
                else
                {
                    _waitForFreeOperationSlot = waitTask;
                    waitTask.GetAwaiter().UnsafeOnCompleted(_startCoreAction ??= StartCore);
                }
            }
            else
            {
                ridTask.GetAwaiter().UnsafeOnCompleted(_onRemoteIdReadyAction ??= StartCommonWhenCompleted);
                Invoker.BeginAcquireRemoteId();
            }
        }
        catch (Exception e) when (!isSynchronous)
        {
            Complete(e);
        }
    }

    private void StartCore()
    {
        if (_waitForFreeOperationSlot is { } task)
        {
            _waitForFreeOperationSlot = null;
            try
            {
                task.GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Complete(e);
                return;
            }
        }

        Debug.Assert(RequestWriter is { } && Connection is { } && Invoker is { } && Invoker.RemoteId.IsCompleted);

        Invoker.RegisterOperation(this);

        var writer = RequestWriter;
        RequestWriter = null;

        _message.OperationId = new OperationId(Invoker.Id, Token);
        _message.RemoteId = Invoker.RemoteId.GetAwaiter().GetResult();

        _message.ReplacePlaceholders(writer);

        if (Connection.Pools.TryGetCompressor() is { } compressor)
        {
            var compressedWriter = Connection.Pools.GetWriter();
            _message.Compress(writer, compressor, compressedWriter);
            Connection.Pools.Return(writer);
            Connection.Pools.Return(compressor);
            writer = compressedWriter;
        }

        if (CancellationToken.CanBeCanceled)
        {
            _cancellationTokenRegistration = CancellationToken.UnsafeRegister(static state =>
            {
                Debug.Assert(state is { });
                ((InvokerOperation)state).Cancel();
            }, this);
        }

        Connection.Dispatch(writer);
    }

    public void Complete(ref SequenceReader<byte> responseBody)
    {
        if (Interlocked.CompareExchange(ref _isResultSet, 1, 0) == 0)
        {
            CompleteCore(ref responseBody);
        }
    }

    protected abstract void CompleteCore(ref SequenceReader<byte> responseBody);

    public void Complete(Exception e)
    {
        if (Interlocked.CompareExchange(ref _isResultSet, 1, 0) == 0)
        {
            CompleteCore(e);
        }
    }

    protected abstract void CompleteCore(Exception e);

    private void Cancel()
    {
        Debug.Assert(Connection is { } && SerializationContext is { } && Invoker is { });

        Connection.Dispatch(new ExecuteCancelNotification
        {
            OperationId = new OperationId(Invoker.Id, Token),
        });
    }
}
