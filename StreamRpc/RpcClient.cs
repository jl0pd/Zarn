using Microsoft.Extensions.DependencyInjection;
using StreamRpc.Protocol;
using StreamRpc.Serialization;

namespace StreamRpc;

public sealed class RpcClient : IAsyncDisposable, IServiceProvider
{
    private Task? _connectionTask;
    private ConnectionContext? _connection;
    private readonly IRpcClientStrategy _strategy;
    private CancellationTokenSource? _cts;

    public RpcClient(RpcStreamProvider streamProvider, BinarySerializationSettings? settings = null)
    : this(new ClientRpcClientStrategy(streamProvider, settings))
    {
    }

    internal RpcClient(IRpcClientStrategy strategy)
    {
        _strategy = strategy;
    }

    public async ValueTask ConnectAsync(CancellationToken cancellationToken)
    {
        if (_connection is { } || _connectionTask is { } || _cts is { })
        {
            throw new InvalidOperationException("Client has already started");
        }

        _cts = cancellationToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : new CancellationTokenSource();

        var token = _cts.Token;

        _connection = await _strategy.ConnectAsync(token);
        _connectionTask = Task.WhenAll(_connection.SendMessages(token),
                                       _connection.ReadMessages(token));
        if (_connectionTask.IsCompleted)
        {
            await _connectionTask; // it has failed
        }
    }

    public void ConfigureServices(Action<IServiceCollection> configure)
    {
        _strategy.ConfigureServices(configure);
    }

    public async ValueTask DisposeAsync()
    {
        await _strategy.DisposeAsync();
        _connection = null;
        if (Interlocked.Exchange(ref _cts, null) is { } cts)
        {
            await cts.CancelAsync();
            cts.Dispose();
        }
        if (_connectionTask is { } connTask)
        {
            await connTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    public object? GetService(Type serviceType)
    {
        if (_connection is null)
        {
            ObjectDisposedException.ThrowIf(_connectionTask is { }, this);
            throw new InvalidOperationException("Client has not started yet");
        }

        if (serviceType == typeof(IServiceProvider))
        {
            return this;
        }
        else if (serviceType.IsConstructedGenericType && serviceType.GetGenericTypeDefinition() == typeof(IRemote<>))
        {
            var genArg = serviceType.GetGenericArguments()[0];
            var invoker = _connection.GetInvoker(genArg);

            var remoteT = typeof(Remote<>).MakeGenericType(genArg);
            return Activator.CreateInstance(remoteT, [invoker]);
        }
        else
        {
            return _strategy.Services.GetService(serviceType);
        }
    }
}
