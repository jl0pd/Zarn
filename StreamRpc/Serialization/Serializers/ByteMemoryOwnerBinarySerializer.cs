using System.Buffers;

namespace StreamRpc.Serialization.Serializers;

internal sealed class ByteMemoryOwnerBinarySerializer : BinarySerializer<IMemoryOwner<byte>?>
{
    public override IMemoryOwner<byte>? Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        int length = context.Deserialize<int>(ref source);
        if (length == -1)
        {
            return null;
        }
        else if (length == 0)
        {
            return new EmptyMemoryOwner<byte>();
        }
        else if (length < -1)
        {
            throw new InvalidDataException();
        }
        else
        {
            var result = context.ToMemory(source.UnreadSequence.Slice(0, length));
            source.Advance(length);
            return result;
        }
    }

    public override void Serialize(IMemoryOwner<byte>? value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        if (value is null)
        {
            context.Serialize(-1, writer);
            return;
        }

        var span = value.Memory.Span;
        if (span.Length == 0)
        {
            context.Serialize(0, writer);
        }
        else
        {
            context.Serialize(span.Length, writer);
            writer.Write(span);
        }
    }
}
