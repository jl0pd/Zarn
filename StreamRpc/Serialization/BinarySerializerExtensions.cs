using System.Buffers;

namespace StreamRpc.Serialization;

public static class BinarySerializerExtensions
{
    public static byte[] SerializeToByteArray<T>(this BinarySerializer<T> serializer, T value, BinarySerializationContext context)
    {
        var writer = new ArrayBufferWriter<byte>();
        serializer.Serialize(value, writer, context);
        return writer.WrittenSpan.ToArray();
    }

    public static void Reserve(this IBufferWriter<byte> writer, int reservedSize)
    {
        writer.GetSpan(reservedSize);
        writer.Advance(reservedSize);
    }
}
