using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class NotSupportedExceptionBinarySerializer : ExceptionSerializerBase<NotSupportedException>
{
    public static NotSupportedExceptionBinarySerializer Instance { get; } = new();

    protected override NotSupportedException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new NotSupportedException(message, innerException);
    }

    protected override void SerializeProps(NotSupportedException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
