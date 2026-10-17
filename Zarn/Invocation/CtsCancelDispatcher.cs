using Zarn.Protocol;

namespace Zarn.Invocation;

internal sealed class CtsCancelDispatcher : IThreadPoolWorkItem
{
    public CancellationTokenSource? CancellationTokenSource { get; set; }

    public ConnectionContext? Connection { get; set; }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
            Justification = "Exception will be dispatched using connection.Fail")]
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
        catch (Exception e)
        {
            connection.Fail(e);
        }
    }
}
