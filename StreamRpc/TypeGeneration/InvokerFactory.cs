using System.Reflection;
using StreamRpc.Protocol;

namespace StreamRpc.TypeGeneration;

internal sealed class InvokerFactory
{
    public Type InterfaceType { get; }

    public Type ImplementationType { get; }

    public MethodInfo?[] MethodsTable { get; }

    public InvokerFactory(InterfaceDescriptor descriptor)
    {
        InterfaceType = descriptor.ResolveType() ?? throw new InvalidOperationException("Unable to load interface: " + descriptor.AssemblyQualifiedName);
        MethodsTable = descriptor.ResolveMethods();
        ImplementationType = InvokerImplementer.GetImplementation(InterfaceType);
    }

    public InvokerBase GetInvoker()
    {
        var invoker = (InvokerBase)Activator.CreateInstance(ImplementationType)!;
        invoker.MethodSlots = MethodsTable;
        return invoker;
    }

    public InvokerBase GetInvoker(Type[] genericArgs)
    {
        var invoker = (InvokerBase)Activator.CreateInstance(ImplementationType.MakeGenericType(genericArgs))!;
        invoker.MethodSlots = MethodsTable;
        return invoker;
    }
}
