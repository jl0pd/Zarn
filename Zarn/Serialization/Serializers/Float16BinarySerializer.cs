using System.Buffers;
using System.Buffers.Binary;

namespace Zarn.Serialization.Serializers;

internal sealed class Float16BinarySerializer : BinarySerializer<Half>
{
    public static Float16BinarySerializer Instance { get; } = new();

    public override Half Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        var currentSpan = source.UnreadSpan;
        if (currentSpan.Length >= 2)
        {
            var result = BinaryPrimitives.ReadHalfLittleEndian(currentSpan);
            source.Advance(2);
            return result;
        }
        else
        {
            Span<byte> span = stackalloc byte[2];
            source.UnreadSequence.Slice(0, 2).CopyTo(span);
            var result = BinaryPrimitives.ReadHalfLittleEndian(span);
            source.Advance(2);
            return result;
        }
    }

    public override void Serialize(Half value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        var span = writer.GetSpan(2);
        BinaryPrimitives.WriteHalfLittleEndian(span, value);
        writer.Advance(2);
    }
}
