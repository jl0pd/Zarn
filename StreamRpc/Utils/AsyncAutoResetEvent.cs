using System.Threading.Tasks.Sources;

namespace StreamRpc.Utils;

// Note: this type is not safe for concurrent waiting, at most 1 waiter may exist
internal sealed class AsyncAutoResetEvent
{
    private readonly Source _source = new();

    public void Set()
    {
        _source.SetResult();
    }

    public ValueTask WaitAsync()
    {
        return new ValueTask(_source, _source.Token);
    }

    private sealed class Source : IValueTaskSource
    {
        private ManualResetValueTaskSourceCore<object?> _source;
        private bool _isResultSet = false;
        private readonly Lock _lock = new();

        public void SetResult()
        {
            lock (_lock)
            {
                if (!_isResultSet)
                {
                    _isResultSet = true;
                    _source.SetResult(this); // set `this` just to distinct it from unset value in debugger
                }
            }
        }

        public short Token => _source.Version;

        public void GetResult(short token)
        {
            lock (_lock)
            {
                _ = _source.GetResult(token);
                _isResultSet = false;
                _source.Reset();
            }
        }

        public ValueTaskSourceStatus GetStatus(short token) => _source.GetStatus(token);

        public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            => _source.OnCompleted(continuation, state, token, flags);
    }
}
