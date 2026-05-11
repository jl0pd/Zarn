using System.Diagnostics.CodeAnalysis;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Zarn.Collections;
using Zarn.Compression;
using Zarn.Invocation;
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

    public async ValueTask<ConnectionContext> ConnectAsync(CancellationToken cancellationToken)
    {
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

    private async Task<ConnectionContext> ConnectCore(Stream stream, CancellationToken cancellationToken)
    {
        Services = _serviceCollection.BuildServiceProvider();

        var message = _pools.GetWriter();

        var request = new HandshakeRequestMessage
        {
            ProtocolVersionMajor = 1,
            ProtocolVersionMinor = 0,
            SupportedCompressions = _settings.CompressionProviders.Select(x => x.AlgorithmName).ToArray(),
            AllowMinorVersionMismatch = _settings.AllowMinorVersionMismatch,
            Interfaces = Services
                            .GetRequiredService<AllowedRemoteConnections>()
                            .Concat(CommunicationServices.Types)
                            .Select(InterfaceDescriptor.FromType)
                            .ToArray(),
        };
        message.Reserve(PackedInt.MaxSize);
        request.Serialize(message, _serializationContext);

        await StreamHelper.Send(stream, message, cancellationToken);

        message.Reset();
        var initialBuffer = new byte[PackedInt.MaxSize];
        if (!await StreamHelper.Read(stream, initialBuffer, message, cancellationToken))
        {
            throw new EndOfStreamException("Could not read handshake message from stream");
        }

        var reader = message.GetReader();
        var response = HandshakeResponseMessage.Deserialize(ref reader, _serializationContext);
        _pools.Return(message);

        if (response.ErrorCode != ErrorCode.Ok)
        {
            throw new ProtocolViolationException("An error occurred on attempt to establish connection: " + response.ErrorCode);
        }

        if (response.IsLittleEndian != BitConverter.IsLittleEndian)
        {
            throw new NotSupportedException("Server has different endianness");
        }

        var compressionProvider = GetCompression(response.ChosenCompression);

        var pools = new Pools(_pools, request.Interfaces, response.Interfaces, compressionProvider);
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
        if (Services is IAsyncDisposable asyncDis)
        {
            return asyncDis.DisposeAsync();
        }
        else if (Services is IDisposable dis)
        {
            dis.Dispose();
        }

        Services = NullServiceProvider.Instance;
        return ValueTask.CompletedTask;
    }
}
