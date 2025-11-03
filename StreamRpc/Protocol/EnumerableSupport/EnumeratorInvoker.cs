using System.Collections;
using System.Diagnostics;
using StreamRpc.Utils;

namespace StreamRpc.Protocol.EnumerableSupport;

internal sealed class EnumeratorInvoker<T> : FinalizableInvokerBase, IEnumerator<T>, IAsyncEnumerator<T>
{
    public T Current { get; internal set; } = default!;

    object? IEnumerator.Current => Current;

    private MoveNextInvokerOperation<T> CreateMoveNextOperation()
    {
        var op = State.Connection.Pools.GetMoveNextInvokerOperation<T>();
        op.Invoker = State;
        op.Prepare();
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
        op.SerializeArg((int)EnumeratorMethod.Dispose);
        SynchronousWaitVoidResult(op.Start());
    }

    public ValueTask DisposeAsync()
    {
        var op = CreateVoidOperation();
        op.SerializeArg((int)EnumeratorMethod.DisposeAsync);
        return op.Start();
    }

    public bool MoveNext()
    {
        var op = CreateMoveNextOperation();
        op.SerializeArg((int)EnumeratorMethod.MoveNext);
        return SynchronousWaitResult(op.Start(this));
    }

    public ValueTask<bool> MoveNextAsync()
    {
        var op = CreateMoveNextOperation();
        op.SerializeArg((int)EnumeratorMethod.MoveNextAsync);
        return op.Start(this);
    }

    public void Reset()
    {
        var op = CreateVoidOperation();
        op.SerializeArg((int)EnumeratorMethod.Reset);
        SynchronousWaitVoidResult(op.Start());
    }
}
