using System.Buffers;

namespace Zarn.Serialization.Serializers.Exceptions;

internal sealed class DivideByZeroExceptionBinarySerializer : ExceptionSerializerBase<DivideByZeroException>
{
    public static DivideByZeroExceptionBinarySerializer Instance { get; } = new();

    protected override DivideByZeroException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new DivideByZeroException(message, innerException);
    }

    protected override void SerializeProps(DivideByZeroException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
