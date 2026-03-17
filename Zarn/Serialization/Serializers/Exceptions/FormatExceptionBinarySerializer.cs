using System.Buffers;

namespace Zarn.Serialization.Serializers.Exceptions;

internal sealed class FormatExceptionBinarySerializer : ExceptionSerializerBase<FormatException>
{
    public static FormatExceptionBinarySerializer Instance { get; } = new();

    protected override FormatException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new FormatException(message, innerException);
    }

    protected override void SerializeProps(FormatException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
