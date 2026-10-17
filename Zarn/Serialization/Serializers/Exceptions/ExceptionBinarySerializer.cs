using System.Buffers;

namespace Zarn.Serialization.Serializers.Exceptions;

internal sealed class ExceptionBinarySerializer : ExceptionSerializerBase<Exception>
{
    public static ExceptionBinarySerializer Instance { get; } = new();

    [SuppressMessage("Usage", "CA2201:Do not raise reserved exception types",
            Justification = "Someone can raise this exception and we must handle it")]
    protected override Exception DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new Exception(message, innerException);
    }

    protected override void SerializeProps(Exception value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
