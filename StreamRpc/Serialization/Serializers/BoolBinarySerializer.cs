using System.Buffers;

namespace StreamRpc.Serialization.Serializers;

internal sealed class BoolBinarySerializer : BinarySerializer<bool>
{
    public static BoolBinarySerializer Instance { get; } = new();

    public override bool Deserialize(ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        var result = source.FirstSpan[0] != 0;
        source.Advance(1);
        return result;
    }

    public override void Serialize(bool value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        var span = writer.GetSpan();
        span[0] = Convert.ToByte(value);
        writer.Advance(1);
    }
}
