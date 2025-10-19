namespace StreamRpc.Protocol;

internal abstract class OnCompletedWorker
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    protected OnCompletedWorker()
    {
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    public CalleeBase? Callee { get; set; }

    public Action OnCompleted { get; protected set; }
}

internal sealed class VoidOnCompletedWorker : OnCompletedWorker
{
    public VoidOnCompletedWorker()
    {
        OnCompleted = () =>
        {
            var pools = Callee?.Callees?.Pools;
            Callee?.CompleteVoidTask(Task);
            Task = default;
            Callee = null;
            pools?.Return(this);
        };
    }

    public ValueTask Task { get; set; }
}

internal sealed class OnCompletedWorker<T> : OnCompletedWorker
{
    public OnCompletedWorker()
    {
        OnCompleted = () =>
        {
            var pools = Callee?.Callees?.Pools;
            Callee?.CompleteTask(Task);
            Task = default;
            Callee = null;
            pools?.Return(this);
        };
    }

    public ValueTask<T> Task { get; set; }
}
