using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class ArithmeticExceptionBinarySerializer : ExceptionSerializerBase<ArithmeticException>
{
    public static ArithmeticExceptionBinarySerializer Instance { get; } = new();

    protected override ArithmeticException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new ArithmeticException(message, innerException);
    }

    protected override void SerializeProps(ArithmeticException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
