using System.Buffers;

namespace Zarn.Serialization;

/// <summary>
/// Defines type that is able to binary serialize and deserialize itself.
/// </summary>
/// <typeparam name="TSelf">The type that implements the interface.</typeparam>
public interface IBinarySerializable<TSelf> where TSelf : IBinarySerializable<TSelf>
{
    void Serialize(IBufferWriter<byte> writer, BinarySerializationContext context);

    static abstract TSelf Deserialize(ref SequenceReader<byte> reader, BinarySerializationContext context);
}
