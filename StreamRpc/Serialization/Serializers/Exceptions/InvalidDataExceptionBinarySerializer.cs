using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class InvalidDataExceptionBinarySerializer : ExceptionSerializerBase<InvalidDataException>
{
    public static InvalidDataExceptionBinarySerializer Instance { get; } = new();

    protected override InvalidDataException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new InvalidDataException(message, innerException);
    }

    protected override void SerializeProps(InvalidDataException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
