using Microsoft.Extensions.DependencyInjection;
using Zarn.Protocol;
using Zarn.Utils;

namespace Zarn;

public sealed class RpcClient : IAsyncDisposable, IServiceProvider
{
    private Task? _connectionTask;
    private ConnectionContext? _connection;
    private readonly IRpcClientStrategy _strategy;
    private readonly CommunicationMonitor _monitor = new();

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
        cancellationToken.ThrowIfCancellationRequested();

        if (_connection is { } || _connectionTask is { })
        {
            throw new InvalidOperationException("Client has already started");
        }

        await ThreadingHelper.LeaveContext();

        // Given cancellationToken is used only for initial connection.
        // Cancellation after this method completes should not cancel communication.
        using var registration = cancellationToken.UnsafeRegister(
                                        static state => ((CommunicationMonitor?)state)?.Dispose(),
                                        _monitor);

        _connection = await _strategy.ConnectAsync(_monitor.CancellationToken);

        var sendTask = _connection.SendMessages(_monitor.CancellationToken);
        var readTask = _connection.ReadMessages(_monitor.CancellationToken);

        _monitor.AddCompleter(sendTask);
        _monitor.AddCompleter(readTask);

        _connectionTask = Task.WhenAll(sendTask, readTask);

        if (_connectionTask.IsCompleted)
        {
            await _connectionTask; // it has failed
        }
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

        _monitor.Dispose();

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

    private sealed class CommunicationMonitor : IDisposable
    {
        [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed",
                Justification = "Field is disposed in thread-safe way")]
        private CancellationTokenSource? _cts = new();

        public CancellationToken CancellationToken { get; }

        public CommunicationMonitor()
        {
            CancellationToken = _cts.Token;
        }

        public void AddCompleter(Task task)
        {
            _ = task.ContinueWith(_ => Dispose(),
                                  CancellationToken.None,
                                  TaskContinuationOptions.HideScheduler,
                                  TaskScheduler.Default);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _cts, null) is { } cts)
            {
                // this cancellation token is used in a such way that `Cancel` won't ever throw.
                cts.Cancel();
                cts.Dispose();
            }
        }
    }
}
