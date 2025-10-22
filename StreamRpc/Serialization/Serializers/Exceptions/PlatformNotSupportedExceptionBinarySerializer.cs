using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class PlatformNotSupportedExceptionBinarySerializer : ExceptionSerializerBase<PlatformNotSupportedException>
{
    public static PlatformNotSupportedExceptionBinarySerializer Instance { get; } = new();

    protected override PlatformNotSupportedException DeserializeCore(string message, Exception? innerException, ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        return new PlatformNotSupportedException(message, innerException);
    }

    protected override void SerializeProps(PlatformNotSupportedException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
