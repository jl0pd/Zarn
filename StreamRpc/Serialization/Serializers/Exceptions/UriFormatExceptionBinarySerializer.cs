using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class UriFormatExceptionBinarySerializer : ExceptionSerializerBase<UriFormatException>
{
    public static UriFormatExceptionBinarySerializer Instance { get; } = new();

    protected override UriFormatException DeserializeCore(string message, Exception? innerException, ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        return new UriFormatException(message, innerException);
    }

    protected override void SerializeProps(UriFormatException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
