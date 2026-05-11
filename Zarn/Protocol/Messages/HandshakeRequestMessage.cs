using System.Buffers;
using System.Net;
using Zarn.Serialization;

namespace Zarn.Protocol.Messages;

internal sealed class HandshakeRequestMessage : IBinarySerializable<HandshakeRequestMessage>
{
    /// <summary>
    /// Major version of protocol. Must match on client and server
    /// </summary>
    public required int ProtocolVersionMajor { get; set; }

    /// <summary>
    /// Minor version of protocol. May mismatch if <see cref="AllowMinorVersionMismatch"/> is set.
    /// </summary>
    public required int ProtocolVersionMinor { get; set; }

    public required bool AllowMinorVersionMismatch { get; set; }

    public required string[] SupportedCompressions { get; set; } = [];

    public required InterfaceDescriptor[] Interfaces { get; set; } = [];

    public static HandshakeRequestMessage Deserialize(ref SequenceReader<byte> reader, BinarySerializationContext context)
    {
        var messageType = context.Deserialize<MessageType>(ref reader);
        if (messageType != MessageType.HandshakeRequest)
        {
            throw new ProtocolViolationException($"Expected handshake request, got {messageType}");
        }
        return new HandshakeRequestMessage
        {
            ProtocolVersionMajor = context.Deserialize<int>(ref reader),
            ProtocolVersionMinor = context.Deserialize<int>(ref reader),
            AllowMinorVersionMismatch = context.Deserialize<bool>(ref reader),
            SupportedCompressions = context.Deserialize<string[]>(ref reader),
            Interfaces = context.Deserialize<InterfaceDescriptor[]>(ref reader),
        };
    }

    public void Serialize(IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(MessageType.HandshakeRequest, writer);
        context.Serialize(ProtocolVersionMajor, writer);
        context.Serialize(ProtocolVersionMinor, writer);
        context.Serialize(AllowMinorVersionMismatch, writer);
        context.Serialize(SupportedCompressions, writer);
        context.Serialize(Interfaces, writer);
    }
}
