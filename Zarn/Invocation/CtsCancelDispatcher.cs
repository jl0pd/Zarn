using System.Diagnostics;
using Zarn.Protocol;

namespace Zarn.Invocation;

internal sealed class CtsCancelDispatcher : IThreadPoolWorkItem
{
    public CancellationTokenSource? CancellationTokenSource { get; set; }

    public ConnectionContext? Connection { get; set; }

    public void Execute()
    {
        Debug.Assert(Connection is { } && CancellationTokenSource is { });

        var cts = CancellationTokenSource;
        var connection = Connection;

        CancellationTokenSource = null;
        Connection = null;

        connection.Pools.Return(this);

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (AggregateException e)
        {
            var exception = connection.Settings.WrapException(e);
            Debug.Fail(null);
            throw new NotImplementedException(null, e);
        }
    }
}
