namespace StreamRpc.Tests;

internal sealed class TestServerRpcStreamProvider(Stream? stream) : RpcStreamProvider
{
    public override async ValueTask<Stream> OpenStreamAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref stream, null) is { } s)
        {
            return s;
        }
        else
        {
            return default!;
        }
    }
}
