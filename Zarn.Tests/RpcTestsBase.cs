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
        await RunConnectToServerTestCore(() =>
        {
            var (serverStream, clientStream) = FullDuplexStream.CreatePair();
            return (RpcStreamProvider.FromStream(serverStream), RpcStreamProvider.FromStream(clientStream));
        },
        services =>
        {
            services.AddTransient<TInterface, TImpl>();
            services.AllowRemoteConnection<TInterface>();
        },
        services =>
        {
        },
        async client =>
        {
            var remoteImpl = client.GetRemoteService<TInterface>();
            await assert(remoteImpl);

            for (int i = 0; i < 3; i++)
            {
                await assert(remoteImpl);
            }
        }, settings);
    }

    protected static async Task RunConnectToServerTestCore(
                                    Func<(RpcStreamProvider ServerStream, RpcStreamProvider ClientStream)> streamFactory,
                                    Action<IServiceCollection> configureServer,
                                    Action<IServiceCollection> configureClient,
                                    Func<RpcClient, Task> handleClient,
                                    RpcSettings settings)
    {
        var (serverStream, clientStream) = streamFactory();

        await using var server = new RpcServer(serverStream, settings);

        server.ConfigureServices(configureServer);

        await using var client = new RpcClient(clientStream, settings);

        client.ConfigureServices(configureClient);

        var serverClientTask = server.AcceptSingleClient(TestContext.Current.CancellationToken);

        await client.ConnectAsync(TestContext.Current.CancellationToken);
        var serverClient = await serverClientTask;

        await handleClient(client);

        await client.DisposeAsync();

        await client.CommunicationEnd;
        await serverClient.CommunicationEnd;
    }
}
