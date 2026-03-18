using System.Buffers;

namespace Zarn.Serialization.Serializers.Exceptions;

internal sealed class OutOfMemoryExceptionBinarySerializer : ExceptionSerializerBase<OutOfMemoryException>
{
    public static OutOfMemoryExceptionBinarySerializer Instance { get; } = new();

    protected override OutOfMemoryException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new OutOfMemoryException(message, innerException);
    }

    protected override void SerializeProps(OutOfMemoryException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
