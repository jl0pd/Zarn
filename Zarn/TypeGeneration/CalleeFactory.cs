using System.Diagnostics;
using Zarn.Protocol;

namespace Zarn.TypeGeneration;

internal sealed class CalleeFactory
{
    public Type InterfaceType { get; }
    public Type ImplementationType { get; }

    private readonly List<(Type[] GenType, ICalleeFactory Factory)> _genericFactories = [];
    private ICalleeFactory? _nonGenericFactory;
    private readonly Lock _genFactoriesLock = new();

    public CalleeFactory(InterfaceDescriptor descriptor)
    {
        var interfaceType = descriptor.ResolveType();
        Debug.Assert(interfaceType is { }, "Local type must always be possible to resolve");
        Debug.Assert(interfaceType.IsInterface);
        InterfaceType = interfaceType;
        ImplementationType = CalleeImplementer.GetImplementation(interfaceType);
    }

    public ICalleeFactory? TryGetFactory(Type interfaceType)
    {
        Debug.Assert(!interfaceType.IsGenericType);
        if (interfaceType == InterfaceType)
        {
            return Volatile.Read(ref _nonGenericFactory)
                ?? Interlocked.CompareExchange(ref _nonGenericFactory, new CompiledCalleeFactory(ImplementationType), null)
                ?? _nonGenericFactory;
        }

        return null;
    }

    public ICalleeFactory? TryGetFactory(Type interfaceType, Type[] genericArgs)
    {
        Debug.Assert(interfaceType.IsGenericTypeDefinition);
        Debug.Assert(genericArgs.Length > 0);
        if (interfaceType == InterfaceType)
        {
            var genArgsSpan = genericArgs.AsSpan();
            lock (_genFactoriesLock)
            {
                foreach (var (genType, factory) in _genericFactories)
                {
                    if (genArgsSpan.SequenceEqual(genType))
                    {
                        return factory;
                    }
                }

                _genericFactories.Add(
                    (genericArgs, new CompiledCalleeFactory(ImplementationType.MakeGenericType(interfaceType)))
                );
            }
        }
        return null;
    }
}
