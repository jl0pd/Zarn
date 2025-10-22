using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class ExceptionBinarySerializer : ExceptionSerializerBase<Exception>
{
    public static ExceptionBinarySerializer Instance { get; } = new();

    protected override Exception DeserializeCore(string message, Exception? innerException, ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        return new Exception(message, innerException);
    }

    protected override void SerializeProps(Exception value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
