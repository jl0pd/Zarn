using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class OverflowExceptionBinarySerializer : ExceptionSerializerBase<OverflowException>
{
    public static OverflowExceptionBinarySerializer Instance { get; } = new();

    protected override OverflowException DeserializeCore(string message, Exception? innerException, ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        return new OverflowException(message, innerException);
    }

    protected override void SerializeProps(OverflowException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
