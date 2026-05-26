using System.Buffers;
using System.Net;
using Zarn.Serialization;

namespace Zarn.Protocol.Messages;

internal readonly struct HandshakeResponseMessage : IMessageInternal<HandshakeResponseMessage>
{
    public MessageType Type => MessageType.HandshakeResponse;

    public bool IsSuccess => ErrorCode == ErrorCode.Ok;

    public required ErrorCode ErrorCode { get; init; }

    public required string? ChosenCompression { get; init; }

    public required InterfaceDescriptor[] Interfaces { get; init; }

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
            ChosenCompression = context.Deserialize<string?>(ref reader),
            Interfaces = context.Deserialize<InterfaceDescriptor[]>(ref reader),
        };
    }

    public void Serialize(IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(MessageType.HandshakeResponse, writer);
        context.Serialize(ErrorCode, writer);
        context.Serialize(ChosenCompression, writer);
        context.Serialize(Interfaces, writer);
    }
}
