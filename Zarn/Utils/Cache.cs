using System.Collections.Concurrent;

namespace Zarn.Utils;

internal sealed class Cache<T> : IDisposable where T : class
{
    private T? _lastValue;
    private readonly int _maxSize;
    private Func<T>? _allocate;
    private readonly Action<T> _free;
    private readonly ConcurrentQueue<T> _queue = [];

    public Cache(int maxSize, Func<T> allocate, Action<T> free)
    {
        ArgumentOutOfRangeException.ThrowIfZero(maxSize);
        ArgumentOutOfRangeException.ThrowIfNegative(maxSize);

        ArgumentNullException.ThrowIfNull(allocate);
        ArgumentNullException.ThrowIfNull(free);

        _maxSize = maxSize - 1; // -1 to account for _lastValue
        _allocate = allocate;
        _free = free;
    }

    public Cache(int maxSize, Func<T> allocate) : this(maxSize, allocate, _ => { })
    {
    }

    public Cache(Func<T> allocate) : this(int.MaxValue, allocate)
    {
    }

    public Cache(Func<T> allocate, Action<T> free) : this(int.MaxValue, allocate, free)
    {
    }

    public T Get()
    {
        ObjectDisposedException.ThrowIf(_allocate is null, this);

        var value = Interlocked.Exchange(ref _lastValue, null);
        if (value is null)
        {
            if (!_queue.TryDequeue(out value))
            {
                value = _allocate();
            }
        }

        return value;
    }

    public void Return(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (_allocate is null)
        {
            _free(value);
            return;
        }

        if (Interlocked.CompareExchange(ref _lastValue, value, null) != null)
        {
            if (_queue.Count < _maxSize)
            {
                _queue.Enqueue(value);
            }
            else
            {
                _free(value);
            }
        }
    }

    public void Dispose()
    {
        _allocate = null!;
        while (_queue.TryDequeue(out var value))
        {
            _free(value);
        }

        if (Interlocked.Exchange(ref _lastValue, null) is { } last)
        {
            _free(last);
        }
    }
}
