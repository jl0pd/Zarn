using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Core;

internal sealed class ByteBinarySerializer : BinarySerializer<byte>
{
    public static ByteBinarySerializer Instance { get; } = new();

    public override byte Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        var result = source.UnreadSpan[0];
        source.Advance(1);
        return result;
    }

    public override void Serialize(byte value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        writer.GetSpan()[0] = value;
        writer.Advance(1);
    }
}
