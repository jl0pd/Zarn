using Microsoft.Extensions.DependencyInjection;
using StreamRpc.Protocol;

namespace StreamRpc;

internal interface IRpcClientStrategy : IAsyncDisposable
{
    public IServiceProvider Services { get; }

    public ValueTask<ConnectionContext> ConnectAsync(CancellationToken cancellationToken);

    public void ConfigureServices(Action<IServiceCollection> configure);
}
