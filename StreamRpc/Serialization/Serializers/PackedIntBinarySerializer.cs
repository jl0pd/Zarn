using System.Buffers;

namespace StreamRpc.Serialization.Serializers;

internal sealed class PackedIntBinarySerializer : BinarySerializer<int>
{
    public static PackedIntBinarySerializer Instance { get; } = new();

    public override int Deserialize(ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        if (PackedInt.TryRead(source.FirstSpan, out long value, out int consumed))
        {
            // value was read from single span, fast path
            source.Advance(consumed);
            return checked((int)value);
        }

        throw new NotImplementedException();
    }

    public override void Serialize(int value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        var size = PackedInt.GetRequiredSize(value);
        var span = writer.GetSpan(size);
        int written = PackedInt.Write(value, span);
        writer.Advance(written);
    }
}
