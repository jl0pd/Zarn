using System.Buffers;
using System.Diagnostics;
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using Benchmarks;
using Benchmarks.Grpc;
using Grpc.Net.Client;
using Zarn;

/// <summary>
/// Measures how much time it take to complete <see cref="Iterations"/> requests
/// when they're processed concurrently
/// </summary>
[MemoryDiagnoser]
public class BatchBenchmarks
{
    [Params(100, 1_000)]
    public int Iterations { get; set; }

    private GrpcChannel? _grpcChannel;
    private Benchmarks.Grpc.Calculator.CalculatorClient? _grpcClient;
    private HttpClient? _httpClient;
    private RpcClient? _rpcClient;
    private ICalculator? _rpcCalculator;

    // used by ZarnWhenAll
    private Memory<ValueTask<int>> _tasks;
    private Memory<int> _results;

    [GlobalSetup]
    public async Task Setup()
    {
        _grpcChannel = GrpcChannel.ForAddress("https://localhost:7123");
        _grpcClient = new Benchmarks.Grpc.Calculator.CalculatorClient(_grpcChannel);

        _httpClient = new HttpClient();

        _rpcClient = new RpcClient(RpcStreamProvider.FromHttp2Endpoint(
                                                    _httpClient,
                                                    new Uri("https://localhost:7123/Zarn/calculator2")));

        await _rpcClient.ConnectAsync(default);
        _rpcCalculator = _rpcClient.GetRemoteService<ICalculator>();

        _tasks = new ValueTask<int>[Iterations];
        _results = new int[Iterations];
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _grpcChannel?.Dispose();
        if (_rpcClient is { })
        {
            await _rpcClient.DisposeAsync();
        }
        _httpClient?.Dispose();
    }

    // grpc doesn't support multiple concurrent requests, so linearize them
    [Benchmark]
    public async Task Grpc()
    {
        var client = _grpcClient ?? throw new InvalidOperationException();

        using var stream = client.AddStreaming();

        var writeTask = Task.Run(async () =>
        {
            var op = new BinaryOperationRequest { Left = 40, Right = 2 };
            for (int i = 0; i < Iterations; i++)
            {
                await stream.RequestStream.WriteAsync(op);
            }

            await stream.RequestStream.CompleteAsync();
        });

        var readTask = Task.Run(async () =>
        {
            while (await stream.ResponseStream.MoveNext(CancellationToken.None))
            {
                _ = stream.ResponseStream.Current;
            }
        });

        await Task.WhenAll(writeTask, readTask);
    }

    [Benchmark]
    public async Task Zarn()
    {
        var calculator = _rpcCalculator ?? throw new InvalidOperationException();

        var channel = Channel.CreateUnbounded<ValueTask<int>>(new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = false,
            SingleReader = true,
            SingleWriter = true,
        });

        var writeTask = Task.Run(async () =>
        {
            for (int i = 0; i < Iterations; i++)
            {
                await channel.Writer.WriteAsync(calculator.Add(40, 2));
            }

            channel.Writer.Complete();
        });

        var readTask = Task.Run(async () =>
        {
            while (await channel.Reader.WaitToReadAsync())
            {
                var success = channel.Reader.TryRead(out var task);
                Debug.Assert(success);
                await task;
            }
        });

        await Task.WhenAll(writeTask, readTask);
    }

    /// <summary>
    /// This benchmark is not honest, but it shows how library can be used when maximum throughput is needed
    /// </summary>
    [Benchmark]
    public async Task ZarnWhenAll()
    {
        var calculator = _rpcCalculator ?? throw new InvalidOperationException();

        for (int i = 0; i < Iterations; i++)
        {
            _tasks.Span[i] = calculator.Add(40, 2);
        }

        await new ValueTaskWhenAll<int>(_tasks, _results);
    }
}
