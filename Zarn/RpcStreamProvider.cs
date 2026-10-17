using System.Net;
using System.Net.Sockets;
using Zarn.Utils;

namespace Zarn;

public abstract class RpcStreamProvider : IAsyncDisposable
{
    public abstract ValueTask<Stream?> OpenStreamAsync(CancellationToken cancellationToken);

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return DisposeAsyncCore();
    }

    protected virtual ValueTask DisposeAsyncCore()
    {
        return ValueTask.CompletedTask;
    }

    public static RpcStreamProvider FromStream(Stream stream)
        => new Singleton(stream ?? throw new ArgumentNullException(nameof(stream)));

    public static RpcStreamProvider FromServerIp(IPEndPoint endpoint)
        => new ClientTcp(endpoint);

    public static RpcStreamProvider FromListenPort(IPEndPoint endpoint)
        => new ListenPort(endpoint);

    public static RpcStreamProvider FromHttp2Endpoint(HttpClient httpClient, Uri address)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(address);

        return new Http2Endpoint(httpClient, address, _ => { });
    }

    public static RpcStreamProvider FromHttp2Endpoint(HttpClient httpClient, Uri address, Action<HttpRequestMessage> configureMessage)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(configureMessage);

        return new Http2Endpoint(httpClient, address, configureMessage);
    }

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
        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
                Justification = "it's disposed in case of fail, otherwise ownership is passed to NetworkStream")]
        public override async ValueTask<Stream?> OpenStreamAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref endpoint, null) is { } ep)
            {
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                
                try
                {
                    await socket.ConnectAsync(ep, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }

                return new NetworkStream(socket, true);
            }

            return null;
        }
    }

    private sealed class ListenPort(IPEndPoint endpoint) : RpcStreamProvider
    {
        [SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", 
                Justification = "Disposed in thread-safe way")]
        private TcpListener? _listener = new(endpoint);

        private bool _isStarted;

        public override async ValueTask<Stream?> OpenStreamAsync(CancellationToken cancellationToken)
        {
            var listener = _listener;
            ObjectDisposedException.ThrowIf(listener is null, this);

            if (!_isStarted)
            {
                listener.Start();
                _isStarted = true;
            }

            var socket = await listener.AcceptSocketAsync(cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, true);
        }

        protected override async ValueTask DisposeAsyncCore()
        {
            await base.DisposeAsyncCore().ConfigureAwait(false);

            Interlocked.Exchange(ref _listener, null)?.Dispose();
        }
    }

    private sealed class Http2Endpoint(HttpClient httpClient, Uri address, Action<HttpRequestMessage> configureMessage) : RpcStreamProvider
    {
        private static readonly Version s_v20 = new(2, 0);

        public override async ValueTask<Stream?> OpenStreamAsync(CancellationToken cancellationToken)
        {
            var outputStreamTcs = new TaskCompletionSource<(Stream, CancellationToken)>();
            var endCommunicationTcs = new TaskCompletionSource();
            using var request = new HttpRequestMessage
            {
                Version = s_v20,
                VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
                Method = HttpMethod.Post,
                RequestUri = address,
                Headers = { TransferEncodingChunked = true },
                Content = new PostStreamContent(async (stream, ct) =>
                {
                    // flush stream even though it's empty to avoid deadlock
                    await stream.FlushAsync(ct).ConfigureAwait(false);
                    outputStreamTcs.SetResult((stream, ct));
                    await endCommunicationTcs.Task.ConfigureAwait(false);
                }),
            };

            configureMessage(request);

            var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var inputStream = await response.Content.ReadAsStreamAsync(CancellationToken.None).ConfigureAwait(false);
            var (outputStream, outputCt) = await outputStreamTcs.Task.ConfigureAwait(false);
            return new CombinedStream(inputStream, new UnclosableStream(outputStream), endCommunicationTcs);
        }

        // https://github.com/davidfowl/StreamingSample/blob/020357917831f1e74432277b0a95be4e11050ddb/client/PostStreamContent.cs
        private sealed class PostStreamContent(Func<Stream, CancellationToken, Task> generator) : HttpContent
        {
            protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
                => generator(stream, CancellationToken.None);

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
                => generator(stream, cancellationToken);

            protected override bool TryComputeLength(out long length)
            {
                length = -1;
                return false;
            }
        }
    }
}
