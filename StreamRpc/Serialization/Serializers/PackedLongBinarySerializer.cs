using System.Buffers;

namespace StreamRpc.Serialization.Serializers;

internal sealed class PackedLongBinarySerializer : BinarySerializer<long>
{
    public static PackedLongBinarySerializer Instance { get; } = new();

    public override long Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        if (PackedInt.TryRead(source.UnreadSpan, out long value, out int consumed))
        {
            // value was read from single span, fast path
            source.Advance(consumed);
            return value;
        }

        throw new NotImplementedException();
    }

    public override void Serialize(long value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        var size = PackedInt.GetRequiredSize(value);
        var span = writer.GetSpan(size);
        int written = PackedInt.Write(value, span);
        writer.Advance(written);
    }
}
