using System.Buffers;
using System.Diagnostics;
using System.Text;

namespace StreamRpc.Serialization.Serializers.Core;

internal sealed class StringBinarySerializer : BinarySerializer<string?>
{
    internal static StringBinarySerializer Instance { get; } = new();

    internal override byte[] TypePrefix { get; } = [(byte)ObjectType.String];

    public override string? Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        int length = PackedIntBinarySerializer.Instance.Deserialize(ref source, context);
        if (length == -1)
        {
            return null;
        }
        else if (length == 0)
        {
            return "";
        }
        else if (length < -1)
        {
            throw new InvalidDataException();
        }
        else
        {
            var result = Encoding.UTF8.GetString(source.UnreadSequence.Slice(0, length));
            source.Advance(length);
            return result;
        }
    }

    public override void Serialize(string? value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        if (value is null)
        {
            PackedIntBinarySerializer.Instance.Serialize(-1, writer, context);
        }
        else if (value == "")
        {
            PackedIntBinarySerializer.Instance.Serialize(0, writer, context);
        }
        else
        {
            int maxBytes = Encoding.UTF8.GetMaxByteCount(value.Length);

            var bytes = ArrayPool<byte>.Shared.Rent(maxBytes);
            try
            {
                bool success = Encoding.UTF8.TryGetBytes(value, bytes, out int written);
                Debug.Assert(success, "We've rented enough bytes, there shouldn't be an error");

                PackedIntBinarySerializer.Instance.Serialize(written, writer, context);
                bytes.AsSpan(0, written).CopyTo(writer.GetSpan(written));
                writer.Advance(written);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
        }
    }
}
