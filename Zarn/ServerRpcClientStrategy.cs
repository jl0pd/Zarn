using Microsoft.Extensions.DependencyInjection;
using Zarn.Compression;
using Zarn.Protocol;
using Zarn.Serialization;

namespace Zarn;

internal sealed class ServerRpcClientStrategy(Stream stream,
                                              Pools pools,
                                              AsyncServiceScope serviceScope,
                                              InterfaceDescriptor[] interfaceDescriptors,
                                              RpcSettings settings) : IRpcClientStrategy
{
    public IServiceProvider Services => serviceScope.ServiceProvider;

    private CompressionProvider? GetCompression(string[] names)
    {
        foreach (var name in names)
        {
            foreach (var provider in settings.CompressionProviders)
            {
                if (provider.AlgorithmName == name)
                {
                    return provider;
                }
            }
        }

        return null;
    }

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

        ErrorCode errorCode;
        if (request.ProtocolVersionMajor != 1)
        {
            errorCode = ErrorCode.ProtocolMajorVersionMismatch;
        }
        else if (request.ProtocolVersionMinor != 0 && (!settings.AllowMinorVersionMismatch || !request.AllowMinorVersionMismatch))
        {
            errorCode = ErrorCode.ProtocolMinorVersionMismatch;
        }
        else
        {
            errorCode = ErrorCode.Ok;
        }

        var chosenCompression = GetCompression(request.SupportedCompressions);
        var response = new HandshakeResponseMessage
        {
            IsLittleEndian = BitConverter.IsLittleEndian,
            ChosenCompression = chosenCompression?.AlgorithmName,
            Interfaces = interfaceDescriptors,
            ErrorCode = errorCode,
        };

        message.Reserve(PackedInt.MaxSize);
        response.Serialize(message, pools.SerializationContext);

        await StreamHelper.Send(stream, message, cancellationToken);
        pools.Return(message);

        if (!response.IsSuccess)
        {
            throw new InvalidOperationException($"Connection with client failed due to error: {response.ErrorCode}");
        }

        var connPools = new Pools(pools, response.Interfaces, request.Interfaces, chosenCompression);
        return new ConnectionContext(stream, connPools, settings, Services);
    }

    public async ValueTask DisposeAsync()
    {
        await serviceScope.DisposeAsync();
        await stream.DisposeAsync();
    }
}
