using System.Diagnostics;
using System.Reflection;
using StreamRpc.Protocol;
using StreamRpc.Utils;

namespace StreamRpc.TypeGeneration;

internal sealed class InvokerFactory
{
    public Type InterfaceType { get; }

    public Type ImplementationType { get; }

    public MethodInfo?[] MethodsTable { get; }

    private readonly Cache<InvokerBase> _cache;

    public InvokerFactory(InterfaceDescriptor descriptor)
    {
        InterfaceType = descriptor.ResolveType() ?? throw new InvalidOperationException("Unable to load interface: " + descriptor.AssemblyQualifiedName);
        MethodsTable = descriptor.ResolveMethods();
        ImplementationType = InvokerImplementer.GetImplementation(InterfaceType);

        _cache = new Cache<InvokerBase>(() =>
        {
            var invoker = (InvokerBase)Activator.CreateInstance(ImplementationType)!;
            invoker.MethodSlots = MethodsTable;
            return invoker;
        });
    }

    public InvokerBase GetInvoker()
    {
        return _cache.Get();
    }

    public InvokerBase GetInvoker(Type[] genericArgs)
    {
        return _cache.Get();
    }

    public void Return(InvokerBase invoker)
    {
        Debug.Assert(invoker.ImplementedInterface == InterfaceType);
        _cache.Return(invoker);
    }
}
