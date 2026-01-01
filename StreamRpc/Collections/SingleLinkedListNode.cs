namespace StreamRpc.Collections;

internal sealed class SingleLinkedListNode<T>(T value)
{
    public T Value = value;
    public SingleLinkedListNode<T>? Next;
}
