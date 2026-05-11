using System.Buffers;
using System.Diagnostics;
using System.Threading.Tasks.Sources;

namespace Zarn.Invocation;

internal sealed class InvokerOperation<T> : InvokerOperation, IValueTaskSource<T>
{
    private ManualResetValueTaskSourceCore<T> _tcs = new() { RunContinuationsAsynchronously = true };

    public ValueTask<T> Start()
    {
        StartCommon();
        return new ValueTask<T>(this, _tcs.Version);
    }

    protected override void CompleteCore(ref SequenceReader<byte> responseBody)
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
