using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class IndexOutOfRangeExceptionBinarySerializer : ExceptionSerializerBase<IndexOutOfRangeException>
{
    public static IndexOutOfRangeExceptionBinarySerializer Instance { get; } = new();

    protected override IndexOutOfRangeException DeserializeCore(string message, Exception? innerException, ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        return new IndexOutOfRangeException(message, innerException);
    }

    protected override void SerializeProps(IndexOutOfRangeException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
