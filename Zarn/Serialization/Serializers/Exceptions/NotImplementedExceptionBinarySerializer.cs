using System.Buffers;

namespace Zarn.Serialization.Serializers.Exceptions;

internal sealed class NotImplementedExceptionBinarySerializer : ExceptionSerializerBase<NotImplementedException>
{
    public static NotImplementedExceptionBinarySerializer Instance { get; } = new();

    protected override NotImplementedException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new NotImplementedException(message, innerException);
    }

    protected override void SerializeProps(NotImplementedException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
