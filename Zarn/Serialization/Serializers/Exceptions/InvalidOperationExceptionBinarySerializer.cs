using System.Buffers;

namespace Zarn.Serialization.Serializers.Exceptions;

internal sealed class InvalidOperationExceptionBinarySerializer : ExceptionSerializerBase<InvalidOperationException>
{
    public static InvalidOperationExceptionBinarySerializer Instance { get; } = new();

    protected override InvalidOperationException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new InvalidOperationException(message, innerException);
    }

    protected override void SerializeProps(InvalidOperationException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
