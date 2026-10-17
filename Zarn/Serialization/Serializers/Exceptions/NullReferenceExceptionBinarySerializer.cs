using System.Buffers;

namespace Zarn.Serialization.Serializers.Exceptions;

internal sealed class NullReferenceExceptionBinarySerializer : ExceptionSerializerBase<NullReferenceException>
{
    public static NullReferenceExceptionBinarySerializer Instance { get; } = new();

    [SuppressMessage("Usage", "CA2201:Do not raise reserved exception types",
            Justification = "Someone can raise this exception and we must handle it")]
    protected override NullReferenceException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new NullReferenceException(message, innerException);
    }

    protected override void SerializeProps(NullReferenceException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
