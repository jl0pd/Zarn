using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class SystemExceptionBinarySerializer : ExceptionSerializerBase<SystemException>
{
    public static SystemExceptionBinarySerializer Instance { get; } = new();

    protected override SystemException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new SystemException(message, innerException);
    }

    protected override void SerializeProps(SystemException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
