using System.Diagnostics;
using System.IO.Pipes;
using Benchmarks;
using Benchmarks.Grpc;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using StreamRpc;

public static class Program
{
    const int WarmupIterations = 10_000;
    const int IterationsCount = 100_000;

    public static async Task Main(string[] args)
    {
        if (args.Length == 0 || args.SequenceEqual(["console"]))
        {
            await ConsoleMode();
        }
        else if (args.SequenceEqual(["grpc"]))
        {
            await GrpcMode();
        }
        else if (args.SequenceEqual(["grpc-stream"]))
        {
            await GrpcStreamMode();
        }
        else if (args.SequenceEqual(["https"]))
        {
            await HttpsMode();
        }
        else if (args.SequenceEqual(["https-batch"]))
        {
            await HttpsBatchMode();
        }
        else if (args.SequenceEqual(["https-echo"]))
        {
            await HttpsEchoMode();
        }
        else if (args.SequenceEqual(["https-get"]))
        {
            await HttpsGetMode();
        }
        else
        {
            throw new FormatException("Invalid args: " + string.Join(" ", args));
        }
    }

    private static async Task GrpcMode()
    {
        var channel = GrpcChannel.ForAddress("https://localhost:7123");

        var client = new Benchmarks.Grpc.Calculator.CalculatorClient(channel);

        for (int i = 0; i < WarmupIterations; i++)
        {
            await client.AddAsync(new BinaryOperationRequest { Left = 40, Right = 2 });
        }

        await Task.Delay(100);

        var sw = Stopwatch.StartNew();
        var op = new BinaryOperationRequest { Left = 40, Right = 2 };
        for (int i = 0; i < IterationsCount; i++)
        {
            await client.AddAsync(op);
        }

        Console.WriteLine(sw.Elapsed.TotalMilliseconds);
    }

    private static async Task GrpcStreamMode()
    {
        var channel = GrpcChannel.ForAddress("https://localhost:7123");

        var client = new Benchmarks.Grpc.Calculator.CalculatorClient(channel);

        {
            using var stream = client.AddStreaming();

            var writeTask = Task.Run(async () =>
            {
                for (int i = 0; i < WarmupIterations; i++)
                {
                    await stream.RequestStream.WriteAsync(new BinaryOperationRequest { Left = 40, Right = 2 });
                }

                await stream.RequestStream.CompleteAsync();
            });

            await foreach (var response in stream.ResponseStream.ReadAllAsync())
            {
            }

            await writeTask;
        }

        await Task.Delay(100);

        var sw = Stopwatch.StartNew();

        {
            using var stream = client.AddStreaming();

            var writeTask = Task.Run(async () =>
            {
                for (int i = 0; i < IterationsCount; i++)
                {
                    await stream.RequestStream.WriteAsync(new BinaryOperationRequest { Left = 40, Right = 2 });
                }

                await stream.RequestStream.CompleteAsync();
            });

            await foreach (var response in stream.ResponseStream.ReadAllAsync())
            {
            }

            await writeTask;
        }
        Console.WriteLine(sw.Elapsed.TotalMilliseconds);
    }

    private static async Task HttpsMode()
    {
        using var httpClient = new HttpClient();

        await using var client = new RpcClient(RpcStreamProvider.FromHttp2Endpoint(
                                                    httpClient,
                                                    new Uri("https://localhost:7123/streamRpc/calculator2")));

        await client.ConnectAsync(default);

        var calculator = client.GetRemoteService<ICalculator>();

        for (int i = 0; i < WarmupIterations; i++)
        {
            await calculator.Add(40, 2);
        }

        await Task.Delay(100);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < IterationsCount; i++)
        {
            await calculator.Add(40, 2);
        }

        Console.WriteLine(sw.Elapsed.TotalMilliseconds);
    }

    private static async Task HttpsBatchMode()
    {
        using var httpClient = new HttpClient();

        await using var client = new RpcClient(RpcStreamProvider.FromHttp2Endpoint(
                                                    httpClient,
                                                    new Uri("https://localhost:7123/streamRpc/calculator2")));

        await client.ConnectAsync(default);

        var calculator = client.GetRemoteService<ICalculator>();

        for (int i = 0; i < WarmupIterations; i++)
        {
            await calculator.Add(40, 2);
        }

        await Task.Delay(100);

        var sw = Stopwatch.StartNew();
        var tasks = new List<Task>();
        for (int i = 0; i < IterationsCount; i++)
        {
            tasks.Add(calculator.Add(40, 2).AsTask());
        }

        await Task.WhenAll(tasks);

        Console.WriteLine(sw.Elapsed.TotalMilliseconds);
    }

    private static async Task HttpsEchoMode()
    {
        using var httpClient = new HttpClient();

        var streamProvider = RpcStreamProvider.FromHttp2Endpoint(
                                httpClient,
                                new Uri("https://localhost:7123/echo"));

        for (int j = 0; j < 2; j++)
        {
            Console.WriteLine($"Starting {j}");

            await using var stream = await streamProvider.OpenStreamAsync(CancellationToken.None);
            Debug.Assert(stream is { });

            var response = new byte[sizeof(int)];
            for (int i = 0; i < 3; i++)
            {
                await stream.WriteAsync(BitConverter.GetBytes(i));
                await stream.FlushAsync();
                await stream.ReadExactlyAsync(response);

                Console.WriteLine(BitConverter.ToInt32(response));
            }


            await stream.DisposeAsync();
            //var readTask = stream.ReadAsync(response).AsTask();
            //int read = await readTask;
            //if (read != 0)
            //{
            //    throw new InvalidOperationException();
            //}
        }

        Console.WriteLine("Complete all");
    }

    private static async Task HttpsGetMode()
    {
        using var httpClient = new HttpClient();

        var msg = new HttpRequestMessage(HttpMethod.Get, new Uri("https://localhost:7123"));
        msg.Version = new Version(2, 0);

        var response = await (await httpClient.SendAsync(msg)).Content.ReadAsStringAsync();
        Console.WriteLine("Got: " + response);
    }

    private static async Task ConsoleMode()
    {
        ThreadPool.GetMinThreads(out _, out int completionPortThreads);
        ThreadPool.SetMinThreads(Environment.ProcessorCount, completionPortThreads);
        await Task.Delay(1000);

        await RunConnectToServerTest<ICalculator, Benchmarks.Calculator>(async adder =>
        {
            for (int i = 0; i < WarmupIterations; i++)
            {
                await adder.Add(40, 2);
            }

            await Task.Delay(100);

            var list = new List<Task>(IterationsCount);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < IterationsCount; i++)
            {
                await adder.Add(40, 2);
                //list.Add(adder.Add(40, 2).AsTask());
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

        _ = server.AcceptSingleClient();

        await client.ConnectAsync(CancellationToken.None);

        var remoteImpl = client.GetRemoteService<TInterface>();
        await assert(remoteImpl);
    }
}
