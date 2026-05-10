using System.Collections;

namespace Zarn.Collections;

internal sealed class SingleLinkedListNode<T>(T value) : IEnumerable<T>
{
    public T Value = value;
    public SingleLinkedListNode<T>? Next;

    public IEnumerator<T> GetEnumerator()
    {
        var current = this;
        while (current is { })
        {
            yield return current.Value;
            current = current.Next;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
