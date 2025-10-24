using System.Net;
using Microsoft.Extensions.DependencyInjection;
using StreamRpc.Protocol;
using StreamRpc.Serialization;
using StreamRpc.Utils;

namespace StreamRpc;

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
            SupportedCompressions = [],
            AllowMinorVersionMismatch = _settings.AllowMinorVersionMismatch,
            Interfaces = Services
                            .GetRequiredService<AllowedRemoteConnections>()
                            .Select(InterfaceDescriptor.FromType)
                            .ToArray(),
        };
        request.Serialize(message, _serializationContext);

        await StreamHelper.Send(stream, MessageOptions.None, message, cancellationToken);

        message.Reset();
        var initialBuffer = new byte[PackedInt.MaxSize];
        if (!await StreamHelper.Read(stream, initialBuffer, message, cancellationToken))
        {
            throw new EndOfStreamException("Could not read handshake message from stream");
        }

        var response = MessageBase.ReadMessage<HandshakeResponseMessage>(message, _serializationContext);
        _pools.Return(message);

        if (response.Error != ErrorCode.Ok)
        {
            throw new ProtocolViolationException("An error occurred on attempt to establish connection: " + response.Error);
        }

        if (response.IsLittleEndian != BitConverter.IsLittleEndian)
        {
            throw new NotSupportedException("Server has different endianness");
        }

        if (response.ChosenCompression != null)
        {
            throw new NotSupportedException("Server request compression, which is not currently supported");
        }

        if (!response.Options.HasFlag(MessageOptions.Success))
        {
            throw new ProtocolViolationException("Unknown error has occurred");
        }

        return new ConnectionContext(stream, new Pools(_pools, request.Interfaces, response.Interfaces), Services);
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
