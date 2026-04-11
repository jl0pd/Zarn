using Zarn.Utils;

internal sealed class IndexedCompletionAction
{
    public static Cache<IndexedCompletionAction> Cache { get; } = new(() => new());

    public int Index { get; set; }

    public Action OnCompleted { get; }

    public Action<int>? Callback { get; set; }

    public IndexedCompletionAction()
    {
        OnCompleted = Complete;
    }

    private void Complete()
    {
        Callback?.Invoke(Index);
        Cache.Return(this);
    }
}
