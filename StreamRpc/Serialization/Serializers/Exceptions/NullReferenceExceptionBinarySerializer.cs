using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class NullReferenceExceptionBinarySerializer : ExceptionSerializerBase<NullReferenceException>
{
    public static NullReferenceExceptionBinarySerializer Instance { get; } = new();

    protected override NullReferenceException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new NullReferenceException(message, innerException);
    }

    protected override void SerializeProps(NullReferenceException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
