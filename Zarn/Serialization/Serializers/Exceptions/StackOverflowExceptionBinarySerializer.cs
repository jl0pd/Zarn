using System.Buffers;

namespace Zarn.Serialization.Serializers.Exceptions;

// this exception cannot be handled if thrown by runtime, but someone could throw this in own interpreter
internal sealed class StackOverflowExceptionBinarySerializer : ExceptionSerializerBase<StackOverflowException>
{
    public static StackOverflowExceptionBinarySerializer Instance { get; } = new();

    [SuppressMessage("Usage", "CA2201:Do not raise reserved exception types",
            Justification = "Someone can raise this exception and we must handle it")]
    protected override StackOverflowException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new StackOverflowException(message, innerException);
    }

    protected override void SerializeProps(StackOverflowException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
