using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class InvalidCastExceptionBinarySerializer : ExceptionSerializerBase<InvalidCastException>
{
    public static InvalidCastExceptionBinarySerializer Instance { get; } = new();

    protected override InvalidCastException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new InvalidCastException(message, innerException);
    }

    protected override void SerializeProps(InvalidCastException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
