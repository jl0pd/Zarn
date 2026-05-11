using System.Collections;
using System.Diagnostics;

namespace Zarn.Protocol.EnumerableSupport;

internal sealed class EnumerableInvoker<T> : FinalizableInvokerBase, IEnumerable<T>, IAsyncEnumerable<T>
{
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var invoker = GetInvoker(true);

        if (cancellationToken.CanBeCanceled)
        {
            invoker.SubscribeToken(cancellationToken);
        }

        return invoker;
    }

    public IEnumerator<T> GetEnumerator()
    {
        EnumeratorInvoker<T> invoker = GetInvoker(false);

        return invoker;
    }

    private EnumeratorInvoker<T> GetInvoker(bool isAsync)
    {
        Debug.Assert(State.RemoteId.IsCompleted, "RemoteId must be set because invoker is received from remote");
        var invoker = new EnumeratorInvoker<T>()
        {
            State = new EnumeratorInvokerState(State.Connection, State.RemoteId.GetAwaiter().GetResult(), isAsync, typeof(T))
            {
                Id = State.Connection.GenObjectId(),
            },
        };

        State.Connection.InstanceManager.RegisterInvoker(invoker);
        return invoker;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
