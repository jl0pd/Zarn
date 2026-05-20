using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Zarn.Tests.TestTypes;

namespace Zarn.Tests;

public sealed class ConnectionTests : RpcTestsBase
{
    private async Task TestUsingGreeter(Func<(RpcStreamProvider, RpcStreamProvider)> streamFactory)
    {
        await RunConnectToServerTestCore(
                streamFactory,
                services =>
                {
                    services.AddTransient<IGreeter, Greeter>();
                    services.AllowRemoteConnection<IGreeter>();
                },
                services =>
                {
                },
                async client =>
                {
                    var greeter = client.GetRemoteService<IGreeter>();

                    var result = await greeter.GetGreetingAsync("Rpc");
                    Assert.Equal("Hello, Rpc", result);
                },
                new RpcSettings()
            );
    }

    [Fact]
    public async Task TestTcpConnection()
    {
        await TestUsingGreeter(() =>
        {
            var endpoint = IPEndPoint.Parse("127.0.0.1:8085");

            return (RpcStreamProvider.FromListenPort(endpoint), RpcStreamProvider.FromServerIp(endpoint));
        });
    }
}
