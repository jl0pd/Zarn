using System.Diagnostics;
using Zarn.Protocol;
using Zarn.Protocol.Messages;

namespace Zarn.EnumerableSupport;

internal sealed class GetEnumeratorDispatcher : IThreadPoolWorkItem
{
    public ConnectionContext? Connection { get; set; }

    public ObjectId InvokerId { get; set; }

    public ObjectId EnumerableId { get; set; }

    public Type? TypeArg { get; set; }

    public bool IsAsync { get; set; }

    public void Execute()
    {
        Debug.Assert(Connection is { } && TypeArg is { });

        var connection = Connection;
        var invokerId = InvokerId;
        var enumerableId = EnumerableId;
        var genArg = TypeArg;
        var isAsync = IsAsync;

        Connection = null;
        TypeArg = null;

        connection.Pools.Return(this);

        Exception? exception = null;
        object? enumerator = null;
        CancellationTokenSource? cts = null;
        ObjectId enumeratorId = default;

        try
        {
            var enumerable = connection.InstanceManager.GetDescriptor(enumerableId).Instance;
            if (isAsync)
            {
                cts = connection.Pools.GetCts();
                enumerator = GetEnumeratorCache.GetAsyncEnumerator(enumerable, genArg, cts.Token);
            }
            else
            {
                enumerator = GetEnumeratorCache.GetEnumerator(enumerable, genArg);
            }
        }
        catch (Exception e)
        {
            exception = connection.Settings.WrapException(e);
            if (cts is { })
            {
                connection.Pools.Return(cts);
            }
        }

        if (enumerator is { })
        {
            enumeratorId = connection.InstanceManager.Register(enumerator, GetEnumeratorCache.GetFactory(genArg), cts);
        }

        connection.Dispatch(new CreateInstanceMessageResponse
        {
            InvokerId = invokerId,
            IsSuccess = exception is null,
            Exception = exception,
            ObjectId = enumeratorId,
        });
    }
}
