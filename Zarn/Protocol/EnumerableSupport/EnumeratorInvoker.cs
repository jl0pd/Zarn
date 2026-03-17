using System.Collections;
using System.Diagnostics;
using Zarn.Utils;

namespace Zarn.Protocol.EnumerableSupport;

internal sealed class EnumeratorInvoker<T> : FinalizableInvokerBase, IEnumerator<T>, IAsyncEnumerator<T>
{
    public T Current { get; internal set; } = default!;

    object? IEnumerator.Current => Current;

    private MoveNextInvokerOperation<T> CreateMoveNextOperation()
    {
        var op = State.Connection.Pools.GetMoveNextInvokerOperation<T>();
        op.Invoker = State;
        return op;
    }

    internal void SubscribeToken(CancellationToken cancellationToken)
    {
        Debug.Assert(cancellationToken.CanBeCanceled);

        cancellationToken.UnsafeRegister(static state =>
        {
            Debug.Assert(state is { });
            var invoker = (EnumeratorInvoker<T>)state;

            var remoteId = invoker.State.RemoteId.GetAwaiter().GetResult();
            invoker.State.Connection.RemoteInstanceManager.CancelAsyncEnumerator(remoteId).AsTask().Fire();
        }, this);
    }

    public void Dispose()
    {
        var op = CreateVoidOperation();
        op.MethodSlot = (int)EnumeratorMethod.Dispose;
        op.Prepare();
        SynchronousWaitVoidResult(op.Start());
    }

    public ValueTask DisposeAsync()
    {
        var op = CreateVoidOperation();
        op.MethodSlot = (int)EnumeratorMethod.DisposeAsync;
        op.Prepare();
        return op.Start();
    }

    public bool MoveNext()
    {
        var op = CreateMoveNextOperation();
        op.MethodSlot = (int)EnumeratorMethod.MoveNext;
        op.Prepare();
        return SynchronousWaitResult(op.Start(this));
    }

    public ValueTask<bool> MoveNextAsync()
    {
        var op = CreateMoveNextOperation();
        op.MethodSlot = (int)EnumeratorMethod.MoveNextAsync;
        op.Prepare();
        return op.Start(this);
    }

    public void Reset()
    {
        var op = CreateVoidOperation();
        op.MethodSlot = (int)EnumeratorMethod.Reset;
        op.Prepare();
        SynchronousWaitVoidResult(op.Start());
    }
}
