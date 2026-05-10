using System.Buffers;

namespace Zarn.Serialization.Serializers.Exceptions;

// this exception cannot be handled if thrown by runtime, but someone could throw this in own interpreter
internal sealed class AccessViolationExceptionBinarySerializer : ExceptionSerializerBase<AccessViolationException>
{
    public static AccessViolationExceptionBinarySerializer Instance { get; } = new();

    protected override AccessViolationException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new AccessViolationException(message, innerException);
    }

    protected override void SerializeProps(AccessViolationException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
