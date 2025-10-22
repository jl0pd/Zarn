using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Sources;
using StreamRpc.Serialization;

namespace StreamRpc.Protocol;

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

    public MessageOptions RequestOptions { get; set; }

    private int _isResultSet = 0;

    private Task? _waitForFreeOperationSlot;
    private Action? _startCoreAction;

    public Pools? Reset()
    {
        _cancellationTokenRegistration.Dispose();
        _cancellationTokenRegistration = default;
        _isResultSet = 0;
        var pools = Invoker?.Connection.Pools;
        Invoker = null;
        return pools;
    }

    public void Prepare()
    {
        Debug.Assert(Connection is { });
        SerializationContext = Connection.SerializationContext;
        var writer = Connection.Pools.GetWriter();
        writer.Reserve(PackedInt.MaxSize);
        SerializationContext.Serialize(MessageOptions.None, writer);
        SerializationContext.Serialize(MessageType.ExecuteRequest, writer);
        writer.Reserve(OperationId.Size);
        RequestWriter = writer;
    }

    public void SerializeArg<T>(T value)
    {
        Debug.Assert(SerializationContext is { } && RequestWriter is { });

        SerializationContext.Serialize(value, RequestWriter);
    }

    protected void StartCommon()
    {
        CancellationToken.ThrowIfCancellationRequested();
        Debug.Assert(RequestWriter is { } && Connection is { } && Invoker is { });

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
                var info = ExceptionDispatchInfo.Capture(e);
                Complete(info.SourceException);
                return;
            }
        }

        Debug.Assert(RequestWriter is { } && Connection is { } && Invoker is { });

        Invoker.RegisterOperation(this);

        var writer = RequestWriter;
        RequestWriter = null;
        var ar = writer.FirstChunkRequired.Array;
        var opIdStart = ar.AsSpan(PackedInt.MaxSize + sizeof(MessageOptions) + sizeof(MessageType));
        MemoryMarshal.Write(opIdStart, new OperationId(Invoker.Id, Token));

        if (CancellationToken.CanBeCanceled)
        {
            _cancellationTokenRegistration = CancellationToken.UnsafeRegister(static state =>
            {
                ((InvokerOperation?)state)?.Cancel();
            }, this);
        }

        Connection.Dispatch(RequestOptions, writer, null);
    }

    public void Complete(ref ReadOnlySequenceReader<byte> responseBody)
    {
        if (Interlocked.CompareExchange(ref _isResultSet, 1, 0) == 0)
        {
            CompleteCore(ref responseBody);
        }
    }

    protected abstract void CompleteCore(ref ReadOnlySequenceReader<byte> responseBody);

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
        var writer = Connection.Pools.GetWriter();
        writer.Reserve(PackedInt.MaxSize);
        SerializationContext.Serialize(MessageOptions.None, writer);
        SerializationContext.Serialize(MessageType.ExecuteCancel, writer);
        SerializationContext.Serialize(new OperationId(Invoker.Id, Token), writer);
        Connection.Dispatch(MessageOptions.None, writer, null);
    }
}

internal sealed class InvokerOperation<T> : InvokerOperation, IValueTaskSource<T>
{
    private ManualResetValueTaskSourceCore<T> _tcs = new() { RunContinuationsAsynchronously = true };

    public ValueTask<T> Start()
    {
        StartCommon();
        return new ValueTask<T>(this, _tcs.Version);
    }

    protected override void CompleteCore(ref ReadOnlySequenceReader<byte> responseBody)
    {
        Debug.Assert(SerializationContext is { });
        var result = SerializationContext.Deserialize<T>(ref responseBody);

        _tcs.SetResult(result);
    }

    protected override void CompleteCore(Exception e)
    {
        _tcs.SetException(e);
    }

    public T GetResult(short token)
    {
        try
        {
            return _tcs.GetResult(token);
        }
        finally
        {
            _tcs.Reset();
            Reset()?.Return(this);
        }
    }

    public ValueTaskSourceStatus GetStatus(short token)
    {
        return _tcs.GetStatus(token);
    }

    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
    {
        _tcs.OnCompleted(continuation, state, token, flags);
    }
}

internal sealed class VoidInvokerOperation : InvokerOperation, IValueTaskSource
{
    private ManualResetValueTaskSourceCore<object?> _tcs = new() { RunContinuationsAsynchronously = true };

    public ValueTask Start()
    {
        StartCommon();
        return new ValueTask(this, _tcs.Version);
    }

    protected override void CompleteCore(ref ReadOnlySequenceReader<byte> responseBody)
    {
        _tcs.SetResult(null);
    }

    protected override void CompleteCore(Exception e)
    {
        _tcs.SetException(e);
    }

    public void GetResult(short token)
    {
        try
        {
            _ = _tcs.GetResult(token);
        }
        finally
        {
            _tcs.Reset();
            Reset()?.Return(this);
        }
    }

    public ValueTaskSourceStatus GetStatus(short token)
    {
        return _tcs.GetStatus(token);
    }

    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
    {
        _tcs.OnCompleted(continuation, state, token, flags);
    }
}
