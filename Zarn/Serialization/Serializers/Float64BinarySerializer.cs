using System.Buffers;
using System.Buffers.Binary;

namespace Zarn.Serialization.Serializers;

internal sealed class Float64BinarySerializer : BinarySerializer<double>
{
    public static Float64BinarySerializer Instance { get; } = new();

    public override double Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        var currentSpan = source.UnreadSpan;
        if (currentSpan.Length >= 8)
        {
            var result = BinaryPrimitives.ReadDoubleLittleEndian(currentSpan);
            source.Advance(8);
            return result;
        }
        else
        {
            Span<byte> span = stackalloc byte[8];
            source.UnreadSequence.Slice(0, 8).CopyTo(span);
            var result = BinaryPrimitives.ReadDoubleLittleEndian(span);
            source.Advance(8);
            return result;
        }
    }

    public override void Serialize(double value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        var span = writer.GetSpan(8);
        BinaryPrimitives.WriteDoubleLittleEndian(span, value);
        writer.Advance(8);
    }
}
