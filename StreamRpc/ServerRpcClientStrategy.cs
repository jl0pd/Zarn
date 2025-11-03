using Microsoft.Extensions.DependencyInjection;
using StreamRpc.Protocol;
using StreamRpc.Serialization;

namespace StreamRpc;

internal sealed class ServerRpcClientStrategy(Stream stream,
                                              Pools pools,
                                              AsyncServiceScope serviceScope,
                                              InterfaceDescriptor[] interfaceDescriptors,
                                              RpcSettings settings) : IRpcClientStrategy
{
    public IServiceProvider Services => serviceScope.ServiceProvider;

    public void ConfigureServices(Action<IServiceCollection> configure)
    {
        throw new InvalidOperationException("Cannot configure services for client received from RpcServer");
    }

    public async ValueTask<ConnectionContext> ConnectAsync(CancellationToken cancellationToken)
    {
        var initialBuffer = new byte[PackedInt.MaxSize];
        var message = pools.GetWriter();
        if (!await StreamHelper.Read(stream, initialBuffer, message, cancellationToken))
        {
            throw new EndOfStreamException("Could not read handshake request from client");
        }

        var reader = message.GetReader();
        var request = HandshakeRequestMessage.Deserialize(ref reader, pools.SerializationContext);
        message.Reset();

        var response = new HandshakeResponseMessage
        {
            IsLittleEndian = BitConverter.IsLittleEndian,
            ChosenCompression = null,
            Interfaces = interfaceDescriptors,
        };

        if (request.ProtocolVersionMajor != 1)
        {
            response.ErrorCode = ErrorCode.ProtocolMajorVersionMismatch;
        }
        else if (request.ProtocolVersionMinor != 0 && (!settings.AllowMinorVersionMismatch || !request.AllowMinorVersionMismatch))
        {
            response.ErrorCode = ErrorCode.ProtocolMinorVersionMismatch;
        }

        message.Reserve(PackedInt.MaxSize);
        response.Serialize(message, pools.SerializationContext);

        await StreamHelper.Send(stream,
                                message,
                                cancellationToken);
        pools.Return(message);

        if (!response.IsSuccess)
        {
            throw new InvalidOperationException($"Connection with client failed due to error: {response.ErrorCode}");
        }

        return new ConnectionContext(stream, new Pools(pools, response.Interfaces, request.Interfaces), settings, Services);
    }

    public async ValueTask DisposeAsync()
    {
        await serviceScope.DisposeAsync();
        await stream.DisposeAsync();
    }
}
