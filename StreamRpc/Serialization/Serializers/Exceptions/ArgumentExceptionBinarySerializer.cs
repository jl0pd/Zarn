using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class ArgumentExceptionBinarySerializer : ExceptionSerializerBase<ArgumentException>
{
    public static ArgumentExceptionBinarySerializer Instance { get; } = new();

    protected override ArgumentException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        var paramName = context.Deserialize<string>(ref source);
        if (innerException is { })
        {
            return new ArgumentException(message, innerException);
        }
        else if (paramName is null)
        {
            return new ArgumentException(message);
        }
        else
        {
            var trailer = $" (Parameter '{paramName}')";
            var clearMessage = message.Replace(trailer, "");

            return new ArgumentException(clearMessage, paramName);
        }
    }

    protected override void SerializeProps(ArgumentException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(value.ParamName, writer);
    }
}
