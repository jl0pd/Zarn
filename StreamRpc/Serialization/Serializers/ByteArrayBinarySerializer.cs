using System.Buffers;

namespace StreamRpc.Serialization.Serializers;

internal sealed class ByteArrayBinarySerializer : BinarySerializer<byte[]?>
{
    public static ByteArrayBinarySerializer Instance { get; } = new();

    internal override byte[] TypePrefix { get; } = [(byte)(ObjectType.Byte | ObjectType.Array)];

    public override byte[]? Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        int length = context.Deserialize<int>(ref source);
        if (length == -1)
        {
            return null;
        }
        else if (length == 0)
        {
            return [];
        }
        else if (length < -1)
        {
            throw new InvalidDataException();
        }
        else
        {
            var result = source.UnreadSequence.Slice(0, length).ToArray();
            source.Advance(length);
            return result;
        }
    }

    public override void Serialize(byte[]? value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        if (value is null)
        {
            context.Serialize(-1, writer);
        }
        else if (value.Length == 0)
        {
            context.Serialize(0, writer);
        }
        else
        {
            context.Serialize(value.Length, writer);
            writer.Write(value);
        }
    }
}
