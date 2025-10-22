using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class HttpIOExceptionBinarySerializer : ExceptionSerializerBase<HttpIOException>
{
    public static HttpIOExceptionBinarySerializer Instance { get; } = new();

    protected override HttpIOException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        var error = context.Deserialize<HttpRequestError>(ref source);
        return new HttpIOException(error, message, innerException);
    }

    protected override void SerializeProps(HttpIOException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(value.HttpRequestError, writer);
    }
}
