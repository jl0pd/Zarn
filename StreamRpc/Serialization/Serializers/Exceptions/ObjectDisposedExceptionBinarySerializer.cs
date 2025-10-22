using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class ObjectDisposedExceptionBinarySerializer : ExceptionSerializerBase<ObjectDisposedException>
{
    public static ObjectDisposedExceptionBinarySerializer Instance { get; } = new();

    protected override ObjectDisposedException DeserializeCore(string message, Exception? innerException, ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        var objectName = context.Deserialize<string>(ref source);
        if (objectName is { })
        {
            return new ObjectDisposedException(objectName, message);
        }
        else
        {
            return new ObjectDisposedException(message, innerException);
        }
    }

    protected override void SerializeProps(ObjectDisposedException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(value.ObjectName, writer);
    }
}
