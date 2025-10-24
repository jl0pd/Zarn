using System.Buffers;
using System.Diagnostics;
using StreamRpc.Serialization;

namespace StreamRpc.Protocol;

internal enum MessageType : byte
{
    HandshakeRequest = 0,
    HandshakeResponse = 1,

    ExecuteRequest = 2,
    ExecuteResponse = 3,
    ExecuteCancel = 4,
}

internal abstract class MessageBase
{
    public abstract MessageType Type { get; }

    public MessageOptions Options { get; set; }

    protected abstract void DeserializeCore(ref SequenceReader<byte> reader, BinarySerializationContext serializationContext);

    protected abstract void SerializeCore(IBufferWriter<byte> writer, BinarySerializationContext serializationContext);

    public void Serialize(IBufferWriter<byte> writer, BinarySerializationContext serializationContext)
    {
        writer.Reserve(PackedInt.MaxSize);
        serializationContext.Serialize(Options, writer);
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
        var options = serializationContext.Deserialize<MessageOptions>(ref reader);
        var type = serializationContext.Deserialize<MessageType>(ref reader);
        MessageBase message = type switch
        {
            MessageType.HandshakeRequest => new HandshakeRequestMessage(),
            MessageType.HandshakeResponse => new HandshakeResponseMessage(),
            _ => throw new InvalidDataException(),
        };
        message.Options = options;
        Debug.Assert(message.Type == type);
        message.Deserialize(ref reader, serializationContext);
        Debug.Assert(reader.Remaining == 0);

        return (T)message;
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

    public bool IsSuccess => Error == ErrorCode.Ok && !Options.HasFlag(MessageOptions.Success);

    public ErrorCode Error { get; set; }

    public bool IsLittleEndian { get; set; }

    public string? ChosenCompression { get; set; }

    public InterfaceDescriptor[] Interfaces { get; set; } = [];

    protected override void DeserializeCore(ref SequenceReader<byte> reader, BinarySerializationContext serializationContext)
    {
        Error = serializationContext.Deserialize<ErrorCode>(ref reader);
        IsLittleEndian = serializationContext.Deserialize<bool>(ref reader);
        ChosenCompression = serializationContext.Deserialize<string?>(ref reader);
        Interfaces = serializationContext.Deserialize<InterfaceDescriptor[]>(ref reader);
    }

    protected override void SerializeCore(IBufferWriter<byte> writer, BinarySerializationContext serializationContext)
    {
        serializationContext.Serialize(Error, writer);
        serializationContext.Serialize(IsLittleEndian, writer);
        serializationContext.Serialize(ChosenCompression, writer);
        serializationContext.Serialize(Interfaces, writer);
    }
}
