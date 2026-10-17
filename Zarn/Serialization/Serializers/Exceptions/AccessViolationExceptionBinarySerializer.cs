using System.Buffers;

namespace Zarn.Serialization.Serializers.Exceptions;

internal sealed class AccessViolationExceptionBinarySerializer : ExceptionSerializerBase<AccessViolationException>
{
    public static AccessViolationExceptionBinarySerializer Instance { get; } = new();

    [SuppressMessage("Usage", "CA2201:Do not raise reserved exception types",
            Justification = "This exception can be thrown by user code and therefore should be handled")]
    protected override AccessViolationException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new AccessViolationException(message, innerException);
    }

    protected override void SerializeProps(AccessViolationException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
