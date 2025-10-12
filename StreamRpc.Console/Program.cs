using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.Extensions.DependencyInjection;
using StreamRpc;
using StreamRpc.Tests.TestTypes;

public static class Program
{
    public static async Task Main()
    {
        ThreadPool.GetMinThreads(out _, out int completionPortThreads);
        ThreadPool.SetMinThreads(Environment.ProcessorCount, completionPortThreads);
        await Task.Delay(1000);

        await RunConnectToServerTest<IAdder, Adder>(async adder =>
        {
            for (int i = 0; i < 10_000; i++)
            {
                await adder.AddValueAsync(40, 2);
            }

            await Task.Delay(1000);

            const int count = 100_000;
            var list = new List<Task>(count);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                await adder.AddValueAsync(40, 2);
                //list.Add(adder.AddValueAsync(40, 2).AsTask());
            }

            //await Task.WhenAll(list);
            Console.WriteLine(sw.Elapsed.TotalMilliseconds);
        });
    }

    static async Task RunConnectToServerTest<TInterface, TImpl>(Func<TInterface, ValueTask> assert)
    where TImpl : class, TInterface
    where TInterface : class
    {
        string pipeName = "streamRpc/" + Guid.NewGuid().ToString("n");

        var serverStream = new NamedPipeServerStream(pipeName, PipeDirection.InOut, -1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var clientStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        var a = serverStream.WaitForConnectionAsync();
        var b = clientStream.ConnectAsync();

        await Task.WhenAll(a, b);

        await using var server = new RpcServer(RpcStreamProvider.FromStream(serverStream));

        server.ConfigureServices(services =>
        {
            services.AddScoped<TInterface, TImpl>();
            services.AllowRemoteConnection<TInterface>();
        });

        await using var client = new RpcClient(RpcStreamProvider.FromStream(clientStream));

        server.Start();

        await client.ConnectAsync(CancellationToken.None);

        var remoteImpl = client.GetRemoteService<TInterface>();
        await assert(remoteImpl);
    }
}
