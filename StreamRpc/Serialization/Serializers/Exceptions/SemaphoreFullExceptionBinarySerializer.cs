using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class SemaphoreFullExceptionBinarySerializer : ExceptionSerializerBase<SemaphoreFullException>
{
    public static SemaphoreFullExceptionBinarySerializer Instance { get; } = new();

    protected override SemaphoreFullException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new SemaphoreFullException(message, innerException);
    }

    protected override void SerializeProps(SemaphoreFullException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
