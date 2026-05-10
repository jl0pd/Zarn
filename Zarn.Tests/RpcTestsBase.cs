using Microsoft.Extensions.DependencyInjection;
using Nerdbank.Streams;
using Zarn.Compression;

namespace Zarn.Tests;

public abstract class RpcTestsBase
{
    protected static async Task RunConnectToServerTest<TInterface, TImpl>(Func<TInterface, Task> assert)
    where TImpl : class, TInterface
    where TInterface : class
    {
        await RunConnectToServerTestCore<TInterface, TImpl>(assert, new RpcSettings());
        await RunConnectToServerTestCore<TInterface, TImpl>(assert, new RpcSettings()
        {
            CompressionProviders =
            {
                new BrotliCompressionProvider(System.IO.Compression.CompressionLevel.SmallestSize),
            }
        });
    }

    protected static async Task RunConnectToServerTestCore<TInterface, TImpl>(Func<TInterface, Task> assert, RpcSettings settings)
    where TImpl : class, TInterface
    where TInterface : class
    {
        var (serverStream, clientStream) = FullDuplexStream.CreatePair();

        await using var server = new RpcServer(RpcStreamProvider.FromStream(serverStream), settings);

        server.ConfigureServices(services =>
        {
            services.AddTransient<TInterface, TImpl>();
            services.AllowRemoteConnection<TInterface>();
        });

        await using var client = new RpcClient(RpcStreamProvider.FromStream(clientStream), settings);

        var serverClientTask = server.AcceptSingleClient();

        await client.ConnectAsync(CancellationToken.None);
        var serverClient = await serverClientTask;

        var remoteImpl = client.GetRemoteService<TInterface>();
        await assert(remoteImpl);

        for (int i = 0; i < 3; i++)
        {
            await assert(remoteImpl);
        }

        await client.DisposeAsync();

        await client.CommunicationEnd;
        await serverClient.CommunicationEnd;
    }
}
