using System.Buffers;

namespace Zarn.Serialization.Serializers.Exceptions;

internal sealed class ApplicationExceptionBinarySerializer : ExceptionSerializerBase<ApplicationException>
{
    public static ApplicationExceptionBinarySerializer Instance { get; } = new();

    [SuppressMessage("Usage", "CA2201:Do not raise reserved exception types",
            Justification = "Someone can raise this exception and we must handle it")]
    protected override ApplicationException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new ApplicationException(message, innerException);
    }

    protected override void SerializeProps(ApplicationException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
