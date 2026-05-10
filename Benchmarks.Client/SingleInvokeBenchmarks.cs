using BenchmarkDotNet.Attributes;
using Benchmarks;
using Benchmarks.Grpc;
using Grpc.Net.Client;
using Zarn;

/// <summary>
/// Measures how much time it takes to complete single request. 
/// This benchmarks also indicator of latency, since new request isn't sent until previous one completes.
/// </summary>
[MemoryDiagnoser]
public class SingleInvokeBenchmarks
{
    private GrpcChannel? _grpcChannel;
    private Benchmarks.Grpc.Calculator.CalculatorClient? _grpcClient;
    private HttpClient? _httpClient;
    private RpcClient? _rpcClient;
    private ICalculator? _rpcCalculator;

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

    [Benchmark]
    public async Task Grpc()
    {
        var client = _grpcClient ?? throw new InvalidOperationException();

        var op = new BinaryOperationRequest { Left = 40, Right = 2 };
        await client.AddAsync(op);
    }

    [Benchmark]
    public async Task Zarn()
    {
        var calculator = _rpcCalculator ?? throw new InvalidOperationException();
        await calculator.Add(40, 2);
    }
}
