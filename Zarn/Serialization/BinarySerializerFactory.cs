using System.Buffers;

namespace Zarn.Serialization;

public abstract class BinarySerializerFactory : BinarySerializer
{
    public abstract BinarySerializer CreateSerializer(Type type);

    internal override byte[] TypePrefix => throw ThrowHelper.Unreachable;

    public sealed override void Serialize(object? value, Type type, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        throw new InvalidOperationException("Serializer factory cannot serialize object on it's own");
    }

    public sealed override object? Deserialize(Type type, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        throw new InvalidOperationException("Serializer factory cannot serialize object on it's own");
    }
}
