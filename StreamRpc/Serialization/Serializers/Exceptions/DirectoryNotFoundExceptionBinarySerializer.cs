using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class DirectoryNotFoundExceptionBinarySerializer : ExceptionSerializerBase<DirectoryNotFoundException>
{
    public static DirectoryNotFoundExceptionBinarySerializer Instance { get; } = new();

    protected override DirectoryNotFoundException DeserializeCore(string message, Exception? innerException, ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        return new DirectoryNotFoundException(message, innerException);
    }

    protected override void SerializeProps(DirectoryNotFoundException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
