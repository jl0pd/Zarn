using System.Buffers;

namespace Zarn.Serialization.Serializers.Exceptions;

internal sealed class IOExceptionBinarySerializer : ExceptionSerializerBase<IOException>
{
    public static IOExceptionBinarySerializer Instance { get; } = new();

    protected override IOException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new IOException(message, innerException);
    }

    protected override void SerializeProps(IOException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
