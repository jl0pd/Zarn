using System.Buffers;
using Zarn.Collections;

namespace Zarn.Serialization;

public static class BinarySerializerExtensions
{
    public static byte[] SerializeToByteArray<T>(this BinarySerializer<T> serializer, T value, BinarySerializationContext context)
    {
        var writer = new ArrayBufferWriter<byte>();
        serializer.Serialize(value, writer, context);
        return writer.WrittenSpan.ToArray();
    }

    internal static void Reserve(this ChunkedArrayPoolBufferWriter<byte> writer, int reservedSize)
    {
        writer.GetSpan(reservedSize);
        writer.FirstChunkRequired.Start += reservedSize;
    }
}
