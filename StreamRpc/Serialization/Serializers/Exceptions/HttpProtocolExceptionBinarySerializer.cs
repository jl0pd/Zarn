using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class HttpProtocolExceptionBinarySerializer : ExceptionSerializerBase<HttpProtocolException>
{
    public static HttpProtocolExceptionBinarySerializer Instance { get; } = new();

    protected override HttpProtocolException DeserializeCore(string message, Exception? innerException, ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        var error = context.Deserialize<long>(ref source);
        return new HttpProtocolException(error, message, innerException);
    }

    protected override void SerializeProps(HttpProtocolException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(value.ErrorCode, writer);
    }
}
