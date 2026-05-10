using System.Buffers;

namespace Zarn.Serialization.Serializers.Exceptions;

internal sealed class PathTooLongExceptionBinarySerializer : ExceptionSerializerBase<PathTooLongException>
{
    public static PathTooLongExceptionBinarySerializer Instance { get; } = new();

    protected override PathTooLongException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new PathTooLongException(message, innerException);
    }

    protected override void SerializeProps(PathTooLongException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
