using System.Buffers;
using System.Diagnostics;
using System.Threading.Tasks.Sources;

namespace Zarn.Protocol.EnumerableSupport;

internal sealed class MoveNextInvokerOperation<T> : InvokerOperation, IValueTaskSource<bool>
{
    private ManualResetValueTaskSourceCore<bool> _tcs = new() { RunContinuationsAsynchronously = true };

    private EnumeratorInvoker<T>? _enumerator = null;

    public ValueTask<bool> Start(EnumeratorInvoker<T> enumerator)
    {
        _enumerator = enumerator;
        StartCommon();
        return new ValueTask<bool>(this, _tcs.Version);
    }

    protected override void CompleteCore(ref SequenceReader<byte> responseBody)
    {
        Debug.Assert(SerializationContext is { } && _enumerator is { });
        var result = SerializationContext.Deserialize<MoveNextResult<T>>(ref responseBody);
        if (result.Success)
        {
            _enumerator.Current = result.Current!;
        }

        _tcs.SetResult(result.Success);
    }

    protected override void CompleteCore(Exception e)
    {
        _tcs.SetException(e);
    }

    public bool GetResult(short token)
    {
        try
        {
            return _tcs.GetResult(token);
        }
        finally
        {
            _tcs.Reset();
            _enumerator = null;
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
