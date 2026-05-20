using System.Buffers;
using System.Buffers.Binary;

namespace Zarn.Serialization.Serializers;

internal sealed class Float32BinarySerializer : BinarySerializer<float>
{
    public static Float32BinarySerializer Instance { get; } = new();

    public override float Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        var currentSpan = source.UnreadSpan;
        if (currentSpan.Length >= 4)
        {
            var result = BinaryPrimitives.ReadSingleLittleEndian(currentSpan);
            source.Advance(4);
            return result;
        }
        else
        {
            Span<byte> span = stackalloc byte[4];
            source.UnreadSequence.Slice(0, 4).CopyTo(span);
            var result = BinaryPrimitives.ReadSingleLittleEndian(span);
            source.Advance(4);
            return result;
        }
    }

    public override void Serialize(float value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        var span = writer.GetSpan(4);
        BinaryPrimitives.WriteSingleLittleEndian(span, value);
        writer.Advance(4);
    }
}
