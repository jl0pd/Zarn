using Microsoft.Extensions.DependencyInjection;
using Zarn.Protocol;

namespace Zarn;

internal interface IRpcClientStrategy : IAsyncDisposable
{
    public IServiceProvider Services { get; }

    public ValueTask<ConnectionContext> ConnectAsync(CancellationToken cancellationToken);

    public void ConfigureServices(Action<IServiceCollection> configure);
}
