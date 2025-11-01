using Microsoft.Extensions.DependencyInjection;
using Nerdbank.Streams;

namespace StreamRpc.Tests;

public abstract class RpcTestsBase
{
    protected static async Task RunConnectToServerTest<TInterface, TImpl>(Func<TInterface, Task> assert)
    where TImpl : class, TInterface
    where TInterface : class
    {
        var (serverStream, clientStream) = FullDuplexStream.CreatePair();

        await using var server = new RpcServer(RpcStreamProvider.FromStream(serverStream));

        server.ConfigureServices(services =>
        {
            services.AddTransient<TInterface, TImpl>();
            services.AllowRemoteConnection<TInterface>();
        });

        await using var client = new RpcClient(RpcStreamProvider.FromStream(clientStream));

        server.Start();

        await client.ConnectAsync(CancellationToken.None);

        var remoteImpl = client.GetRemoteService<TInterface>();
        await assert(remoteImpl);

        for (int i = 0; i < 10; i++)
        {
            await assert(remoteImpl);
        }
    }
}
