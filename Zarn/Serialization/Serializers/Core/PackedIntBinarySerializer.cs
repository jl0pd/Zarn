using System.Buffers;

namespace Zarn.Serialization.Serializers.Core;

internal sealed class PackedIntBinarySerializer : BinarySerializer<int>
{
    public static PackedIntBinarySerializer Instance { get; } = new();

    public override int Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        if (!PackedInt.TryRead(source.UnreadSpan, out long value, out int consumed))
        {
            // value is spanning multiple chunks, slow path
            Span<byte> span = stackalloc byte[consumed];
            source.UnreadSequence.Slice(0, consumed).CopyTo(span);

            if (!PackedInt.TryRead(span, out value, out consumed))
            {
                throw new InvalidDataException();
            }
        }

        source.Advance(consumed);
        return checked((int)value);
    }

    public override void Serialize(int value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        var size = PackedInt.GetRequiredSize(value);
        var span = writer.GetSpan(size);
        int written = PackedInt.Write(value, span);
        writer.Advance(written);
    }
}
