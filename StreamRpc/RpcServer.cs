using Microsoft.Extensions.DependencyInjection;
using StreamRpc.Protocol;
using StreamRpc.Serialization;

namespace StreamRpc;

public sealed class RpcServer : IAsyncDisposable
{
    private readonly RpcStreamProvider _streamProvider;
    private readonly RpcSettings _settings;
    private CancellationTokenSource? _cts = new();
    private readonly CancellationToken _cancellationToken;
    private ServiceProvider? _services;
    private readonly ServiceCollection _serviceDescriptors = [];

    public event EventHandler<ClientConnectedEventArgs>? ClientConnected;
    public event EventHandler<ThreadExceptionEventArgs>? ExceptionOccurred;

    public RpcServer(RpcStreamProvider streamProvider, RpcSettings? settings = null)
    {
        _streamProvider = streamProvider;
        _settings = settings ?? new();
        _cancellationToken = _cts.Token;

        _serviceDescriptors.AddSingleton(new AllowedRemoteConnections());
    }

    public void ConfigureServices(Action<IServiceCollection> configure)
    {
        configure(_serviceDescriptors);
    }

    public void Start()
    {
        if (_services is { })
        {
            throw new InvalidOperationException("Server already has started");
        }

        _services = _serviceDescriptors.BuildServiceProvider();

        _ = Task.Run(async () =>
        {
            var interfaceDescriptors = _services
                                        .GetRequiredService<AllowedRemoteConnections>()
                                        .Concat(CommunicationServices.Types)
                                        .Select(InterfaceDescriptor.FromType)
                                        .ToArray();

            var pools = new Pools(new BinarySerializationContext(_settings));

            while (!_cancellationToken.IsCancellationRequested)
            {
                var stream = await _streamProvider.OpenStreamAsync(_cancellationToken);
                if (stream is null)
                {
                    return;
                }

                var scope = _services.CreateAsyncScope();
                var client = new RpcClient(new ServerRpcClientStrategy(
                                                    stream,
                                                    pools,
                                                    scope,
                                                    interfaceDescriptors,
                                                    _settings));
                await client.ConnectAsync(_cancellationToken);
                ClientConnected?.Invoke(this, new ClientConnectedEventArgs(client));
            }
        })
        .ContinueWith(
            x =>
            {
                ExceptionOccurred?.Invoke(this, new ThreadExceptionEventArgs(x.Exception!));
            },
            TaskContinuationOptions.OnlyOnFaulted);
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _cts, null) is { } cts)
        {
            cts.Cancel();
            cts.Dispose();
        }

        _serviceDescriptors.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        Stop();

        if (Interlocked.Exchange(ref _services, null) is { } sc)
        {
            await sc.DisposeAsync();
        }
    }
}
