using System.Collections;
using System.Runtime.CompilerServices;

namespace StreamRpc.Collections;

internal sealed class FreezableList<T> : IList<T>, IReadOnlyList<T>, IList, ICollection<T>, IReadOnlyCollection<T>, ICollection
{
    public int Count => _list.Count;

    public bool IsReadOnly => _isFrozen.Value;

    public bool IsSynchronized => IsReadOnly;

    public object SyncRoot => this;

    bool IList.IsFixedSize => IsReadOnly;

    object? IList.this[int index]
    {
        get => this[index];
        set => this[index] = Cast(value);
    }

    public T this[int index]
    {
        get => _list[index];
        set
        {
            ThrowIfFrozen();
            _list[index] = value;
        }
    }

    private readonly List<T> _list = [];
    private readonly StrongBox<bool> _isFrozen;

    public FreezableList(StrongBox<bool> isFrozen)
    {
        _isFrozen = isFrozen;
    }

    public FreezableList(StrongBox<bool> isFrozen, IEnumerable<T> source)
    {
        _isFrozen = isFrozen;
        _list = new List<T>(source);
    }

    public int IndexOf(T item) => _list.IndexOf(item);

    public void Insert(int index, T item)
    {
        ThrowIfFrozen();
        _list.Insert(index, item);
    }

    public void RemoveAt(int index)
    {
        ThrowIfFrozen();
        _list.RemoveAt(index);
    }

    public void Add(T item)
    {
        ThrowIfFrozen();
        _list.Add(item);
    }

    public void Clear()
    {
        ThrowIfFrozen();
        _list.Clear();
    }

    public bool Contains(T item) => _list.Contains(item);

    public void CopyTo(T[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);

    public void CopyTo(Array array, int index) => ((ICollection)_list).CopyTo(array, index);

    public bool Remove(T item)
    {
        ThrowIfFrozen();
        return _list.Remove(item);
    }

    private void ThrowIfFrozen()
    {
        if (IsReadOnly)
        {
            throw new InvalidOperationException("List is frozen and cannot be modified");
        }
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_list).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_list).GetEnumerator();

    private static T Cast(object? value) => (T)value!;

    int IList.Add(object? value)
    {
        if (IsReadOnly)
        {
            return -1;
        }
        _list.Add(Cast(value));
        return Count - 1;
    }

    bool IList.Contains(object? value)
    {
        return value is T t && _list.Contains(t);
    }

    int IList.IndexOf(object? value)
    {
        return value is T t ? _list.IndexOf(t) : -1;
    }

    void IList.Insert(int index, object? value)
    {
        Insert(index, Cast(value));
    }

    void IList.Remove(object? value)
    {
        Remove(Cast(value));
    }
}
