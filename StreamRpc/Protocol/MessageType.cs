using System.Buffers;
using System.Diagnostics;
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
}

internal abstract class MessageBase
{
    public abstract MessageType Type { get; }

    protected abstract void DeserializeCore(ref SequenceReader<byte> reader, BinarySerializationContext serializationContext);

    protected abstract void SerializeCore(IBufferWriter<byte> writer, BinarySerializationContext serializationContext);

    public void Serialize(IBufferWriter<byte> writer, BinarySerializationContext serializationContext)
    {
        writer.Reserve(PackedInt.MaxSize);
        serializationContext.Serialize(Type, writer);
        SerializeCore(writer, serializationContext);
    }

    public void Deserialize(ref SequenceReader<byte> reader, BinarySerializationContext serializationContext)
    {
        DeserializeCore(ref reader, serializationContext);
        Debug.Assert(reader.Remaining == 0);
    }

    public static T ReadMessage<T>(ChunkedArrayPoolBufferWriter<byte> chunks, BinarySerializationContext serializationContext) where T : MessageBase
    {
        var reader = chunks.GetReader();
        return ReadMessage<T>(ref reader, serializationContext);
    }

    public static T ReadMessage<T>(ref SequenceReader<byte> reader, BinarySerializationContext serializationContext) where T : MessageBase
    {
        var type = serializationContext.Deserialize<MessageType>(ref reader);
        MessageBase message = type switch
        {
            MessageType.Error => new ErrorMessage(),
            MessageType.HandshakeRequest => new HandshakeRequestMessage(),
            MessageType.HandshakeResponse => new HandshakeResponseMessage(),
            _ => throw new InvalidDataException(),
        };
        Debug.Assert(message.Type == type);
        message.Deserialize(ref reader, serializationContext);
        Debug.Assert(reader.Remaining == 0);

        return (T)message;
    }
}

internal sealed class ErrorMessage : MessageBase
{
    public override MessageType Type => MessageType.Error;

    public ErrorCode ErrorCode { get; set; }

    public string? DetailedMessage { get; set; }

    protected override void DeserializeCore(ref SequenceReader<byte> reader, BinarySerializationContext serializationContext)
    {
        ErrorCode = serializationContext.Deserialize<ErrorCode>(ref reader);
        DetailedMessage = serializationContext.Deserialize<string>(ref reader);
    }

    protected override void SerializeCore(IBufferWriter<byte> writer, BinarySerializationContext serializationContext)
    {
        serializationContext.Serialize(ErrorCode, writer);
        serializationContext.Serialize(DetailedMessage, writer);
    }
}

internal sealed class HandshakeRequestMessage : MessageBase
{
    public override MessageType Type => MessageType.HandshakeRequest;

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

    protected override void DeserializeCore(ref SequenceReader<byte> reader, BinarySerializationContext serializationContext)
    {
        ProtocolVersionMajor = serializationContext.Deserialize<int>(ref reader);
        ProtocolVersionMinor = serializationContext.Deserialize<int>(ref reader);
        AllowMinorVersionMismatch = serializationContext.Deserialize<bool>(ref reader);
        SupportedCompressions = serializationContext.Deserialize<string[]>(ref reader);
        Interfaces = serializationContext.Deserialize<InterfaceDescriptor[]>(ref reader);
    }

    protected override void SerializeCore(IBufferWriter<byte> writer, BinarySerializationContext serializationContext)
    {
        serializationContext.Serialize(ProtocolVersionMajor, writer);
        serializationContext.Serialize(ProtocolVersionMinor, writer);
        serializationContext.Serialize(AllowMinorVersionMismatch, writer);
        serializationContext.Serialize(SupportedCompressions, writer);
        serializationContext.Serialize(Interfaces, writer);
    }
}

internal sealed class HandshakeResponseMessage : MessageBase
{
    public override MessageType Type => MessageType.HandshakeResponse;

    public bool IsSuccess => ErrorCode == ErrorCode.Ok;

    public ErrorCode ErrorCode { get; set; }

    public bool IsLittleEndian { get; set; }

    public string? ChosenCompression { get; set; }

    public InterfaceDescriptor[] Interfaces { get; set; } = [];

    protected override void DeserializeCore(ref SequenceReader<byte> reader, BinarySerializationContext serializationContext)
    {
        ErrorCode = serializationContext.Deserialize<ErrorCode>(ref reader);
        IsLittleEndian = serializationContext.Deserialize<bool>(ref reader);
        ChosenCompression = serializationContext.Deserialize<string?>(ref reader);
        Interfaces = serializationContext.Deserialize<InterfaceDescriptor[]>(ref reader);
    }

    protected override void SerializeCore(IBufferWriter<byte> writer, BinarySerializationContext serializationContext)
    {
        serializationContext.Serialize(ErrorCode, writer);
        serializationContext.Serialize(IsLittleEndian, writer);
        serializationContext.Serialize(ChosenCompression, writer);
        serializationContext.Serialize(Interfaces, writer);
    }
}
