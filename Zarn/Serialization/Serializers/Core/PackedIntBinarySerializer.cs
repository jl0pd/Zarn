using System.Buffers;

namespace Zarn.Serialization.Serializers.Core;

internal sealed class PackedIntBinarySerializer : BinarySerializer<int>
{
    public static PackedIntBinarySerializer Instance { get; } = new();

    public static int DeserializeCore(ref SequenceReader<byte> source, BinarySerializationContext context)
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

    public override int Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return DeserializeCore(ref source, context);
    }

    public static void SerializeCore(int value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        var size = PackedInt.GetRequiredSize(value);
        var span = writer.GetSpan(size);
        int written = PackedInt.Write(value, span);
        writer.Advance(written);
    }

    public override void Serialize(int value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        SerializeCore(value, writer, context);
    }
}
