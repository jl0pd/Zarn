using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class ApplicationExceptionBinarySerializer : ExceptionSerializerBase<ApplicationException>
{
    public static ApplicationExceptionBinarySerializer Instance { get; } = new();

    protected override ApplicationException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new ApplicationException(message, innerException);
    }

    protected override void SerializeProps(ApplicationException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
