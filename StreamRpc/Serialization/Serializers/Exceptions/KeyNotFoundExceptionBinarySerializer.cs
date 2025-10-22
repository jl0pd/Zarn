using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class KeyNotFoundExceptionBinarySerializer : ExceptionSerializerBase<KeyNotFoundException>
{
    public static KeyNotFoundExceptionBinarySerializer Instance { get; } = new();

    protected override KeyNotFoundException DeserializeCore(string message, Exception? innerException, ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        return new KeyNotFoundException(message, innerException);
    }

    protected override void SerializeProps(KeyNotFoundException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
