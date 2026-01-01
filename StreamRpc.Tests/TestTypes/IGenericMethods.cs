using StreamRpc.Tests.Utils;

namespace StreamRpc.Tests.TestTypes;

public interface IGenericMethods
{
    string FreeGeneric<T>();

    T? Default<T>();

    T Id<T>(T value);

    ValueTask<T> IdAsync<T>(T value);

    ValueTask<List<T>> ReplicateToListAsync<T>(T value, int count);

    ValueTask<T[]> ReplicateToArrayAsync<T>(T value, int count);

    ValueTask<T[]> ToArrayAsync<T>(IAsyncEnumerable<T> source);
}

public sealed class GenericMethods : IGenericMethods
{
    public T? Default<T>()
    {
        return default;
    }

    public string FreeGeneric<T>()
    {
        return typeof(T).Name;
    }

    public T Id<T>(T value)
    {
        return value;
    }

    public ValueTask<T> IdAsync<T>(T value)
    {
        return ValueTask.FromResult(value);
    }

    public ValueTask<T[]> ReplicateToArrayAsync<T>(T value, int count)
    {
        return ValueTask.FromResult(Enumerable.Repeat(value, count).ToArray());
    }

    public ValueTask<List<T>> ReplicateToListAsync<T>(T value, int count)
    {
        return ValueTask.FromResult(Enumerable.Repeat(value, count).ToList());
    }

    public ValueTask<T[]> ToArrayAsync<T>(IAsyncEnumerable<T> source)
    {
        return source.ToArrayAsync();
    }
}
