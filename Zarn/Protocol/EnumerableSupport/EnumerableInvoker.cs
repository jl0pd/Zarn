using System.Collections;
using System.Diagnostics;

namespace Zarn.Protocol.EnumerableSupport;

internal sealed class EnumerableInvoker<T> : FinalizableInvokerBase, IEnumerable<T>, IAsyncEnumerable<T>
{
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        Debug.Assert(State.RemoteId.IsCompleted, "RemoteId must be set because invoker is received from remote");

        cancellationToken.ThrowIfCancellationRequested();

        var invoker = new EnumeratorInvoker<T>()
        {
            State = new InvokerState(State.Connection, State.RemoteId.GetAwaiter().GetResult(), true, typeof(T))
            {
                Id = ObjectId.GenObjectId(),
            },
        };

        State.Connection.InstanceManager.RegisterInvoker(invoker);

        if (cancellationToken.CanBeCanceled)
        {
            invoker.SubscribeToken(cancellationToken);
        }

        return invoker;
    }

    public IEnumerator<T> GetEnumerator()
    {
        Debug.Assert(State.RemoteId.IsCompleted, "RemoteId must be set because invoker is received from remote");
        var invoker = new EnumeratorInvoker<T>()
        {
            State = new InvokerState(State.Connection, State.RemoteId.GetAwaiter().GetResult(), false, typeof(T))
            {
                Id = ObjectId.GenObjectId(),
            },
        };

        State.Connection.InstanceManager.RegisterInvoker(invoker);

        return invoker;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
