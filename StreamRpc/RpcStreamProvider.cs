using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;

namespace StreamRpc;

public abstract class RpcStreamProvider : IAsyncDisposable
{
    public abstract ValueTask<Stream?> OpenStreamAsync(CancellationToken cancellationToken);

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public static RpcStreamProvider FromStream(Stream stream)
        => new Singleton(stream ?? throw new ArgumentNullException(nameof(stream)));

    public static RpcStreamProvider FromServerIp(IPEndPoint endpoint)
        => new ClientTcp(endpoint);

    public static RpcStreamProvider FromListenPort(IPEndPoint endpoint)
        => new ListenPort(endpoint);

    private sealed class Singleton(Stream? stream) : RpcStreamProvider
    {
        public override ValueTask<Stream?> OpenStreamAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return ValueTask.FromCanceled<Stream?>(cancellationToken);
            }

            return ValueTask.FromResult(Interlocked.Exchange(ref stream, null));
        }
    }

    private sealed class ClientTcp(IPEndPoint? endpoint) : RpcStreamProvider
    {
        public override async ValueTask<Stream?> OpenStreamAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref endpoint, null) is { } ep)
            {
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(ep, cancellationToken);
                    return new NetworkStream(socket, true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }

            return null;
        }
    }

    private sealed class ListenPort : RpcStreamProvider
    {
        private TcpListener? _listener;

        public ListenPort(IPEndPoint endpoint)
        {
            _listener = new TcpListener(endpoint);
        }

        public override async ValueTask<Stream?> OpenStreamAsync(CancellationToken cancellationToken)
        {
            var listener = _listener;
            ObjectDisposedException.ThrowIf(listener is null, this);

            var socket = await listener.AcceptSocketAsync(cancellationToken);
            return new NetworkStream(socket, true);
        }

        public override ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _listener, null)?.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
