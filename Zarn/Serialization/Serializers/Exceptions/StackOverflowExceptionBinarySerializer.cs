using System.Buffers;

namespace Zarn.Serialization.Serializers.Exceptions;

// this exception cannot be handled if thrown by runtime, but someone could throw this in own interpreter
internal sealed class StackOverflowExceptionBinarySerializer : ExceptionSerializerBase<StackOverflowException>
{
    public static StackOverflowExceptionBinarySerializer Instance { get; } = new();

    protected override StackOverflowException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new StackOverflowException(message, innerException);
    }

    protected override void SerializeProps(StackOverflowException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
