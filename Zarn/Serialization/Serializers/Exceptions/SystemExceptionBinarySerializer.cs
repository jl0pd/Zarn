using System.Buffers;

namespace Zarn.Serialization.Serializers.Exceptions;

internal sealed class SystemExceptionBinarySerializer : ExceptionSerializerBase<SystemException>
{
    public static SystemExceptionBinarySerializer Instance { get; } = new();

    [SuppressMessage("Usage", "CA2201:Do not raise reserved exception types",
            Justification = "Someone can raise this exception and we must handle it")]
    protected override SystemException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new SystemException(message, innerException);
    }

    protected override void SerializeProps(SystemException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
