using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Core;

internal sealed class PackedShortBinarySerializer : BinarySerializer<short>
{
    public static PackedShortBinarySerializer Instance { get; } = new();

    public override short Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        if (PackedInt.TryRead(source.UnreadSpan, out long value, out int consumed))
        {
            // value was read from single span, fast path
            source.Advance(consumed);
            return checked((short)value);
        }

        throw new NotImplementedException();
    }

    public override void Serialize(short value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        var size = PackedInt.GetRequiredSize(value);
        var span = writer.GetSpan(size);
        int written = PackedInt.Write(value, span);
        writer.Advance(written);
    }
}
