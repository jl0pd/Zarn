using System.Runtime.CompilerServices;
using System.Threading.Tasks.Sources;

internal sealed class ValueTaskWhenAll<T> : IValueTaskSource
{
    private ManualResetValueTaskSourceCore<ValueTuple> _task;

    private readonly List<Exception> _exceptions = [];
    private readonly Memory<T> _result;
    private readonly ReadOnlyMemory<ValueTask<T>> _tasks;

    private int _incompleteCount;

    public ValueTaskWhenAll(ReadOnlyMemory<ValueTask<T>> tasks, Memory<T> result)
    {
        _result = result;
        _tasks = tasks;
        _incompleteCount = tasks.Length;

        var span = tasks.Span;
        for (int i = 0; i < span.Length; i++)
        {
            if (span[i].IsCompleted)
            {
                OnCompleted(i);
            }
            else
            {
                var action = IndexedCompletionAction.Cache.Get();
                action.Callback = OnCompleted;
                action.Index = i;
                span[i].GetAwaiter().OnCompleted(action.OnCompleted);
            }
        }
    }

    private void OnCompleted(int index)
    {
        try
        {
            _result.Span[index] = _tasks.Span[index].GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            lock (_exceptions)
            {
                _exceptions.Add(e);
            }
        }

        if (Interlocked.Decrement(ref _incompleteCount) == 0)
        {
            SetResult();
        }
    }

    private void SetResult()
    {
        switch (_exceptions.Count)
        {
            case 0:
                _task.SetResult(ValueTuple.Create());
                break;
            case 1:
                _task.SetException(_exceptions[0]);
                break;
            default:
                _task.SetException(new AggregateException(_exceptions));
                break;
        }
    }

    public ValueTaskAwaiter GetAwaiter() => new ValueTask(this, _task.Version).GetAwaiter();

    public void GetResult(short token)
    {
        _task.GetResult(token);
    }

    public ValueTaskSourceStatus GetStatus(short token)
    {
        return _task.GetStatus(token);
    }

    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
    {
        _task.OnCompleted(continuation, state, token, flags);
    }
}
