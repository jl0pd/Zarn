using System.Runtime.CompilerServices;
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

    public RpcServer(RpcStreamProvider streamProvider, RpcSettings? settings = null)
    {
        _streamProvider = streamProvider;
        _settings = settings ?? new();
        _settings.Freeze();
        _cancellationToken = _cts.Token;

        _serviceDescriptors.AddSingleton(new AllowedRemoteConnections());
    }

    public void ConfigureServices(Action<IServiceCollection> configure)
    {
        configure(_serviceDescriptors);
    }

    /// <summary>
    /// Accepts single client. Server cannot be reused after call to this method.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">
    /// No clients was accepted due to cancellation request
    /// or because <see cref="RpcStreamProvider"/> hasn't provided any stream</exception>
    public async Task<RpcClient> AcceptSingleClient(CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await foreach (var client in Start(cancellationToken))
        {
            cts.Cancel();
            return client;
        }

        throw new InvalidOperationException("No clients was accepted");
    }

    /// <summary>
    /// Begins accepting <see cref="RpcClient"/>s and returns lazy collection of clients.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns>Lazy collection that begins accepting new client when next element is requested.</returns>
    /// <exception cref="InvalidOperationException">Method was called more than once</exception>
    public async IAsyncEnumerable<RpcClient> Start([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_services is { })
        {
            throw new InvalidOperationException("Server has already started");
        }

        cancellationToken.ThrowIfCancellationRequested();

        _services = _serviceDescriptors.BuildServiceProvider();

        var interfaceDescriptors = _services
                                    .GetRequiredService<AllowedRemoteConnections>()
                                    .Concat(CommunicationServices.Types)
                                    .Select(InterfaceDescriptor.FromType)
                                    .ToArray();

        var pools = new Pools(new BinarySerializationContext(_settings));

        using var actualCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationToken);
        var actualCt = actualCts.Token;

        while (!actualCt.IsCancellationRequested)
        {
            var stream = await _streamProvider.OpenStreamAsync(actualCt);
            if (stream is null)
            {
                yield break;
            }

            var scope = _services.CreateAsyncScope();
            var client = new RpcClient(new ServerRpcClientStrategy(
                                                stream,
                                                pools,
                                                scope,
                                                interfaceDescriptors,
                                                _settings));
            await client.ConnectAsync(actualCt);
            yield return client;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _cts, null) is { } cts)
        {
            cts.Cancel();
            cts.Dispose();
        }

        _serviceDescriptors.Clear();

        if (Interlocked.Exchange(ref _services, null) is { } sc)
        {
            await sc.DisposeAsync();
        }
    }
}
