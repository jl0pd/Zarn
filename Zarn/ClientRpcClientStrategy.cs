using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Zarn.Compression;
using Zarn.Protocol;
using Zarn.Protocol.Messages;
using Zarn.Serialization;
using Zarn.Utils;

namespace Zarn;

internal sealed class ClientRpcClientStrategy : IRpcClientStrategy
{
    public IServiceProvider Services { get; private set; } = NullServiceProvider.Instance;

    private readonly ServiceCollection _serviceCollection = [];
    private readonly RpcStreamProvider _streamProvider;
    private readonly RpcSettings _settings;
    private readonly Pools _pools;
    private readonly BinarySerializationContext _serializationContext;

    public ClientRpcClientStrategy(RpcStreamProvider streamProvider, RpcSettings? settings)
    {
        _streamProvider = streamProvider;
        _settings = settings ?? new();
        _settings.Freeze();
        _serializationContext = new BinarySerializationContext(_settings);
        _pools = new Pools(_serializationContext);
        _serviceCollection.AddSingleton(new AllowedRemoteConnections());
    }

    public void ConfigureServices(Action<IServiceCollection> configure)
    {
        configure.Invoke(_serviceCollection);
    }

    [SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task",
            Justification = "LeaveContext is called")]
    public async ValueTask<ConnectionContext> ConnectAsync(CancellationToken cancellationToken)
    {
        await ThreadingHelper.LeaveContext();

        var stream = await _streamProvider.OpenStreamAsync(cancellationToken)
            ?? throw new InvalidOperationException(nameof(RpcStreamProvider) + " has returned null");
        try
        {
            return await ConnectCore(stream, cancellationToken);
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    [SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task",
            Justification = "LeaveContext is called")]
    private async Task<ConnectionContext> ConnectCore(Stream stream, CancellationToken cancellationToken)
    {
        Services = _serviceCollection.BuildServiceProvider();

        var buffer = _pools.GetWriter();

        var (interfaces, classes) = InterfaceDescriptor.CollectDescriptors(Services.GetRequiredService<AllowedRemoteConnections>());
        foreach (var inter in interfaces)
        {
            _serializationContext.AllowType(inter.GetStoredType());
        }
        foreach (var cls in classes)
        {
            _serializationContext.AllowType(cls);
        }

        var request = new HandshakeRequestMessage
        {
            ProtocolVersionMajor = 1,
            ProtocolVersionMinor = 0,
            SupportedCompressions = _settings.CompressionProviders.Select(x => x.AlgorithmName).ToArray(),
            AllowMinorVersionMismatch = _settings.AllowMinorVersionMismatch,
            Interfaces = interfaces,
        };

        await StreamHelper.Send(stream, request, buffer, _serializationContext, cancellationToken);

        buffer.Reset();
        var initialBuffer = new byte[PackedInt.MaxSize];
        if (!await StreamHelper.Read(stream, initialBuffer, buffer, cancellationToken))
        {
            throw new EndOfStreamException("Could not read handshake message from stream");
        }

        var response = IMessageInternal<HandshakeResponseMessage>.Deserialize(buffer, _serializationContext);
        _pools.Return(buffer);

        if (response.ErrorCode != ErrorCode.Ok)
        {
            throw new ProtocolViolationException("An error occurred on attempt to establish connection: " + response.ErrorCode);
        }

        var compressionProvider = GetCompression(response.ChosenCompression);

        var pools = new Pools(_pools, response.Interfaces, request.Interfaces, compressionProvider);
        return new ConnectionContext(false, stream, pools, _settings, Services);
    }

    [return: NotNullIfNotNull(nameof(name))]
    private CompressionProvider? GetCompression(string? name)
    {
        if (name is { })
        {
            foreach (var provider in _settings.CompressionProviders)
            {
                if (provider.AlgorithmName == name)
                {
                    return provider;
                }
            }

            throw new InvalidOperationException($"Server has returned unsupported compression algorithm: {name}");
        }

        return null;
    }

    public ValueTask DisposeAsync()
    {
        _pools.Dispose();

        var svc = Services;
        Services = NullServiceProvider.Instance;

        if (svc is IAsyncDisposable asyncDis)
        {
            return asyncDis.DisposeAsync();
        }
        else if (svc is IDisposable dis)
        {
            dis.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
