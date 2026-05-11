using System.Buffers;
using System.Threading.Tasks.Sources;

namespace Zarn.Invocation;

internal sealed class VoidInvokerOperation : InvokerOperation, IValueTaskSource
{
    private ManualResetValueTaskSourceCore<object?> _tcs = new() { RunContinuationsAsynchronously = true };

    public ValueTask Start()
    {
        StartCommon();
        return new ValueTask(this, _tcs.Version);
    }

    protected override void CompleteCore(ref SequenceReader<byte> responseBody)
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
