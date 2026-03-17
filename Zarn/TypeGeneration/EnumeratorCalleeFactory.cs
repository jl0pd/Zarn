using System.Collections.Concurrent;
using Zarn.Protocol;
using Zarn.Protocol.EnumerableSupport;
using Zarn.Utils;

namespace Zarn.TypeGeneration;

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
