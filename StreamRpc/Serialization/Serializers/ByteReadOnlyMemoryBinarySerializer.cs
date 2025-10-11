using System.Buffers;

namespace StreamRpc.Serialization.Serializers;

internal sealed class ByteReadOnlyMemoryBinarySerializer : BinarySerializer<ReadOnlyMemory<byte>>
{
    public static ByteReadOnlyMemoryBinarySerializer Instance { get; } = new();

    public override ReadOnlyMemory<byte> Deserialize(ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        int length = context.Deserialize<int>(ref source);
        if (length < 0)
        {
            throw new InvalidDataException();
        }

        var result = source.Remaining.Slice(0, length).ToArray();
        source.Advance(length);
        return result;
    }

    public override void Serialize(ReadOnlyMemory<byte> value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(value.Length, writer);
        writer.Write(value.Span);
    }
}
