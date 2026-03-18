using System.Buffers;

namespace Zarn.Serialization.Serializers.Core;

internal sealed class BoolBinarySerializer : BinarySerializer<bool>
{
    public static BoolBinarySerializer Instance { get; } = new();

    public override bool Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        var result = source.UnreadSpan[0] != 0;
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
