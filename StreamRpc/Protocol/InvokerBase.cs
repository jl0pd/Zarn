using System.Diagnostics;
using System.Reflection;

namespace StreamRpc.Protocol;

internal abstract class InvokerBase
{
    internal protected abstract Type ImplementedInterface { get; }

    internal InvokerState State { get; set; } = null!;

    internal MethodInfo?[] MethodSlots { get; set; } = [];

    internal protected int GetMethodSlot(MethodInfo method)
    {
        Debug.Assert(method.IsGenericMethodDefinition || !method.IsGenericMethod);
        var idx = Array.IndexOf(MethodSlots, method);
        if (idx < 0)
        {
            throw new InvalidOperationException("Other side does not support given method: " + method);
        }

        return idx + 1;
    }

    internal protected InvokerOperation<T> CreateOperation<T>()
    {
        var op = State.CreateOperation<T>();
        op.Invoker = State;
        op.Prepare();
        return op;
    }

    internal protected VoidInvokerOperation CreateVoidOperation()
    {
        var op = State.CreateOperation();
        op.Invoker = State;
        op.Prepare();
        return op;
    }

    internal protected static T SynchronousWaitValueResult<T>(ValueTask<T> task)
    {
        var awaiter = task.GetAwaiter();
        if (awaiter.IsCompleted)
        {
            return awaiter.GetResult();
        }
        else
        {
            return task.AsTask().GetAwaiter().GetResult();
        }
    }

    internal protected static void SynchronousWaitVoidValueResult(ValueTask task)
    {
        var awaiter = task.GetAwaiter();
        if (awaiter.IsCompleted)
        {
            awaiter.GetResult();
        }
        else
        {
            task.AsTask().GetAwaiter().GetResult();
        }
    }
}

/// <summary>
/// Specialized object that is created whenever remote proxy is used.
/// </summary>
internal abstract class FinalizableInvokerBase : InvokerBase
{
    ~FinalizableInvokerBase()
    {
        State?.OnCollected();
    }
}
