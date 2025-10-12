using System.Threading.Tasks.Sources;

namespace StreamRpc.Utils;

// Note: this type is not safe for concurrent waiting, at most 1 waiter may exist
internal sealed class AsyncManualResetEvent
{
    private readonly Source _source = new();
    private ValueTask _task;
    private int _isResultSet;

    public bool IsSet => _task.IsCompleted;

    public void Set()
    {
        if (_task.IsCompleted)
        {
            _task = ValueTask.CompletedTask;
        }
        else
        {
            if (Interlocked.Exchange(ref _isResultSet, 1) == 0)
            {
                _source.SetResult();
            }
        }
    }

    public void Reset()
    {
        _source.Reset();
        Volatile.Write(ref _isResultSet, 0);
        _task = new ValueTask(_source, _source.Token);
    }

    public ValueTask WaitAsync()
    {
        return _task;
    }

    private sealed class Source : IValueTaskSource
    {
        private ManualResetValueTaskSourceCore<object?> _source;

        public void SetResult() => _source.SetResult(null);

        public short Token => _source.Version;

        public void Reset() => _source.Reset();

        public void GetResult(short token) => _ = _source.GetResult(token);

        public ValueTaskSourceStatus GetStatus(short token) => _source.GetStatus(token);

        public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            => _source.OnCompleted(continuation, state, token, flags);
    }
}
