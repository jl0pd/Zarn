using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class TimeoutExceptionBinarySerializer : ExceptionSerializerBase<TimeoutException>
{
    public static TimeoutExceptionBinarySerializer Instance { get; } = new();

    protected override TimeoutException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new TimeoutException(message, innerException);
    }

    protected override void SerializeProps(TimeoutException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
