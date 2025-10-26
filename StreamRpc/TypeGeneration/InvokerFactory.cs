using System.Reflection;
using StreamRpc.Protocol;

namespace StreamRpc.TypeGeneration;

internal sealed class InvokerFactory
{
    public Type InterfaceType { get; }

    public Type ImplementationType
        => _implementationType ??= InvokerImplementer.GetImplementation(InterfaceType);
    private Type? _implementationType;

    public Type FinalizableImplementationType
        => _finalizableImplementationType ??= InvokerImplementer.GetFinalizableImplementation(InterfaceType);
    private Type? _finalizableImplementationType;

    public MethodInfo?[] MethodsTable { get; }

    public InvokerFactory(InterfaceDescriptor descriptor)
    {
        InterfaceType = descriptor.ResolveType() ?? throw new InvalidOperationException("Unable to load interface: " + descriptor.AssemblyQualifiedName);
        MethodsTable = descriptor.ResolveMethods();
    }

    public InvokerBase GetInvoker(bool finalizable)
    {
        var type = finalizable ? FinalizableImplementationType : ImplementationType;
        var invoker = (InvokerBase)Activator.CreateInstance(type)!;
        invoker.MethodSlots = MethodsTable;
        return invoker;
    }

    public InvokerBase GetInvoker(bool finalizable, Type[] genericArgs)
    {
        var type = finalizable ? FinalizableImplementationType : ImplementationType;
        var invoker = (InvokerBase)Activator.CreateInstance(type.MakeGenericType(genericArgs))!;
        invoker.MethodSlots = MethodsTable;
        return invoker;
    }
}
