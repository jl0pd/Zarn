using Microsoft.Extensions.DependencyInjection;
using Zarn.Protocol;
using Zarn.Utils;

namespace Zarn;

public sealed class RpcClient : IAsyncDisposable, IServiceProvider
{
    private Task? _connectionTask;
    private ConnectionContext? _connection;
    private readonly IRpcClientStrategy _strategy;

    [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", 
        Justification = "Field is disposed in thread-safe way")]
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Task that can be used to wait for communication to end.
    /// </summary>
    /// <exception cref="InvalidOperationException">An attempt to get task was made while client has not started yet</exception>
    public Task CommunicationEnd
        => _connectionTask ?? throw new InvalidOperationException("Communication has not started yet");

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
        Justification = "False positive, object is stored in field for later disposal")]
    public RpcClient(RpcStreamProvider streamProvider, RpcSettings? settings = null)
    : this(new ClientRpcClientStrategy(streamProvider, settings))
    {
    }

    internal RpcClient(IRpcClientStrategy strategy)
    {
        _strategy = strategy;
    }

    [SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task",
            Justification = "LeaveContext is called")]
    public async ValueTask ConnectAsync(CancellationToken cancellationToken)
    {
        if (_connection is { } || _connectionTask is { } || _cts is { })
        {
            throw new InvalidOperationException("Client has already started");
        }

        await ThreadingHelper.LeaveContext();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var token = _cts.Token;

        _connection = await _strategy.ConnectAsync(token);

        var sendTask = _connection.SendMessages(token);
        var readTask = _connection.ReadMessages(token);

        DisposeIfAnyCompleted(readTask, sendTask);

        _connectionTask = Task.WhenAll(sendTask, readTask);
        if (_connectionTask.IsCompleted)
        {
            await _connectionTask; // it has failed
        }
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
            Justification = "Exception will be handled by different awaiter")]
    [SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task",
            Justification = "Method is called from method without sync ctx")]
    private async void DisposeIfAnyCompleted(Task first, Task second)
    {
        try
        {
            await Task.WhenAny(first, second);
        }
        catch
        {
        }
        await DisposeAsync();
    }

    public void ConfigureServices(Action<IServiceCollection> configure)
    {
        _strategy.ConfigureServices(configure);
    }


    [SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task",
            Justification = "LeaveContext is called")]
    public async ValueTask DisposeAsync()
    {
        await ThreadingHelper.LeaveContext();

        if (Interlocked.Exchange(ref _cts, null) is { } cts)
        {
            await cts.CancelAsync();
            cts.Dispose();
        }
        await _strategy.DisposeAsync();
        if (_connection is { } con)
        {
            await con.DisposeAsync();
            _connection = null;
        }
        if (_connectionTask is { } connTask)
        {
            await connTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

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
            var invoker = _connection.InstanceManager.GetInvoker(genArg, _connection.GenObjectId(), false);

            var remoteT = typeof(Remote<>).MakeGenericType(genArg);
            return Activator.CreateInstance(remoteT, [invoker]);
        }
        else
        {
            return _strategy.Services.GetService(serviceType);
        }
    }
}
