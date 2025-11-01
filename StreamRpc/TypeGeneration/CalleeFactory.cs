using System.Diagnostics;
using System.Reflection;
using StreamRpc.Protocol;
using StreamRpc.Utils;

namespace StreamRpc.TypeGeneration;

internal sealed class CalleeFactory
{
    public Type InterfaceType { get; }
    public Type ImplementationType { get; }

    private readonly Cache<CalleeBase> _cache;

    public MethodInfo[] MethodsTable { get; }

    public CalleeFactory(InterfaceDescriptor descriptor)
    {
        var interfaceType = descriptor.ResolveType();
        Debug.Assert(interfaceType is { }, "Local type must always be possible to resolve");
        Debug.Assert(interfaceType.IsInterface);
        InterfaceType = interfaceType;
        ImplementationType = CalleeImplementer.GetImplementation(interfaceType);
        MethodsTable = interfaceType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
        _cache = new Cache<CalleeBase>(() => (CalleeBase)Activator.CreateInstance(ImplementationType)!);
    }

    public CalleeBase GetCallee()
    {
        return _cache.Get();
    }

    public CalleeBase GetCallee(Type[] typeArgs)
    {
        var actualType = ImplementationType.MakeGenericType(typeArgs);
        return (CalleeBase)Activator.CreateInstance(actualType)!;
    }

    public void Return(CalleeBase callee)
    {
        Debug.Assert(callee.ImplementedInterface == InterfaceType);
        _cache.Return(callee);
    }
}
