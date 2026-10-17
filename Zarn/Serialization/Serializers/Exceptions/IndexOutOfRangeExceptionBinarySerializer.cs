using System.Buffers;

namespace Zarn.Serialization.Serializers.Exceptions;

internal sealed class IndexOutOfRangeExceptionBinarySerializer : ExceptionSerializerBase<IndexOutOfRangeException>
{
    public static IndexOutOfRangeExceptionBinarySerializer Instance { get; } = new();

    [SuppressMessage("Usage", "CA2201:Do not raise reserved exception types",
            Justification = "Someone can raise this exception and we must handle it")]
    protected override IndexOutOfRangeException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new IndexOutOfRangeException(message, innerException);
    }

    protected override void SerializeProps(IndexOutOfRangeException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
