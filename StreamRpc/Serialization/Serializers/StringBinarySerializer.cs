using System.Buffers;
using System.Diagnostics;
using System.Text;

namespace StreamRpc.Serialization.Serializers;

internal sealed class StringBinarySerializer : BinarySerializer<string?>
{
    internal static StringBinarySerializer Instance { get; } = new();

    internal override byte[] TypePrefix { get; } = [(byte)ObjectType.String];

    public override string? Deserialize(ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        int length = context.Deserialize<int>(ref source);
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
            var src = source.Remaining.Slice(0, length);
            source.Advance(length);
            var result = Encoding.UTF8.GetString(src);
            return result;
        }
    }

    public override void Serialize(string? value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        if (value is null)
        {
            context.Serialize(-1, writer);
        }
        else if (value == "")
        {
            context.Serialize(0, writer);
        }
        else
        {
            int maxBytes = Encoding.UTF8.GetMaxByteCount(value.Length);

            var bytes = ArrayPool<byte>.Shared.Rent(maxBytes);
            try
            {
                bool success = Encoding.UTF8.TryGetBytes(value, bytes, out int written);
                Debug.Assert(success, "We've rented enough bytes, there shouldn't be an error");

                context.Serialize(written, writer);
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
