using System.Buffers;

namespace Zarn.Serialization.Serializers.Exceptions;

internal sealed class EndOfStreamExceptionBinarySerializer : ExceptionSerializerBase<EndOfStreamException>
{
    public static EndOfStreamExceptionBinarySerializer Instance { get; } = new();

    protected override EndOfStreamException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new EndOfStreamException(message, innerException);
    }

    protected override void SerializeProps(EndOfStreamException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
