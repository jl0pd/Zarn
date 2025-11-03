using System.Collections.Concurrent;
using StreamRpc.Protocol;
using StreamRpc.Protocol.EnumerableSupport;
using StreamRpc.Utils;

namespace StreamRpc.TypeGeneration;

internal sealed class EnumeratorCalleeFactory
{
    private readonly ConcurrentDictionary<Type, ICalleeFactory> _factories = [];

    public ICalleeFactory GetFactory(Type elementType)
    {
        return _factories.GetOrAdd(elementType, CreateFactory);
    }

    private static ICalleeFactory CreateFactory(Type elementType)
    {
        var type = typeof(EnumeratorCalleeFactory<>).MakeGenericType(elementType);
        return (ICalleeFactory)Activator.CreateInstance(type)!;
    }
}

internal sealed class EnumeratorCalleeFactory<T> : ICalleeFactory
{
    private readonly Cache<CalleeBase> _cache = new(() => new EnumeratorCallee<T>());

    public CalleeBase Get() => _cache.Get();

    public void Return(CalleeBase callee) => _cache.Return(callee);
}
