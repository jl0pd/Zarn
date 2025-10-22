using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class ArrayTypeMismatchExceptionBinarySerializer : ExceptionSerializerBase<ArrayTypeMismatchException>
{
    public static ArrayTypeMismatchExceptionBinarySerializer Instance { get; } = new();

    protected override ArrayTypeMismatchException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new ArrayTypeMismatchException(message, innerException);
    }

    protected override void SerializeProps(ArrayTypeMismatchException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
