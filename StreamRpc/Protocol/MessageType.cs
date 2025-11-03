using System.Buffers;
using System.Net;
using StreamRpc.Serialization;

namespace StreamRpc.Protocol;

internal enum MessageType : byte
{
    Error = 0,

    HandshakeRequest = 1,
    HandshakeResponse = 2,

    ExecuteRequest = 3,
    ExecuteResponse = 4,
    ExecuteCancel = 5,

    GetRemoteIdRequest = 7,
    GetRemoteIdResponse = 8,
}

internal sealed class HandshakeRequestMessage : IBinarySerializable<HandshakeRequestMessage>
{
    /// <summary>
    /// Major version of protocol. Must match on client and server
    /// </summary>
    public int ProtocolVersionMajor { get; set; }

    /// <summary>
    /// Minor version of protocol. May mismatch if <see cref="AllowMinorVersionMismatch"/> is set.
    /// </summary>
    public int ProtocolVersionMinor { get; set; }

    public bool AllowMinorVersionMismatch { get; set; }

    public string[] SupportedCompressions { get; set; } = [];

    public InterfaceDescriptor[] Interfaces { get; set; } = [];

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

internal sealed class HandshakeResponseMessage : IBinarySerializable<HandshakeResponseMessage>
{
    public bool IsSuccess => ErrorCode == ErrorCode.Ok;

    public ErrorCode ErrorCode { get; set; }

    public bool IsLittleEndian { get; set; }

    public string? ChosenCompression { get; set; }

    public InterfaceDescriptor[] Interfaces { get; set; } = [];

    public static HandshakeResponseMessage Deserialize(ref SequenceReader<byte> reader, BinarySerializationContext context)
    {
        var messageType = context.Deserialize<MessageType>(ref reader);
        if (messageType != MessageType.HandshakeResponse)
        {
            throw new ProtocolViolationException($"Expected handshake response, got {messageType}");
        }
        return new HandshakeResponseMessage
        {
            ErrorCode = context.Deserialize<ErrorCode>(ref reader),
            IsLittleEndian = context.Deserialize<bool>(ref reader),
            ChosenCompression = context.Deserialize<string?>(ref reader),
            Interfaces = context.Deserialize<InterfaceDescriptor[]>(ref reader),
        };
    }

    public void Serialize(IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(MessageType.HandshakeResponse, writer);
        context.Serialize(ErrorCode, writer);
        context.Serialize(IsLittleEndian, writer);
        context.Serialize(ChosenCompression, writer);
        context.Serialize(Interfaces, writer);
    }
}
