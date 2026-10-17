using System.Buffers;

namespace Zarn.Serialization.Serializers.Exceptions;

internal sealed class OutOfMemoryExceptionBinarySerializer : ExceptionSerializerBase<OutOfMemoryException>
{
    public static OutOfMemoryExceptionBinarySerializer Instance { get; } = new();

    [SuppressMessage("Usage", "CA2201:Do not raise reserved exception types",
            Justification = "Someone can raise this exception and we must handle it")]
    protected override OutOfMemoryException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new OutOfMemoryException(message, innerException);
    }

    protected override void SerializeProps(OutOfMemoryException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
