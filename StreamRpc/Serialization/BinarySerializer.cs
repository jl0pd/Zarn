using System.Buffers;

namespace StreamRpc.Serialization;

public abstract class BinarySerializer
{
    internal abstract byte[] TypePrefix { get; }

    public abstract bool CanConvert(Type type);

    public abstract void Serialize(object? value, Type type, IBufferWriter<byte> writer, BinarySerializationContext context);

    public abstract object? Deserialize(Type type, ref SequenceReader<byte> source, BinarySerializationContext context);
}
