using System.Buffers;

namespace Zarn.Serialization.Serializers.Exceptions;

internal sealed class ArgumentNullExceptionBinarySerializer : ExceptionSerializerBase<ArgumentNullException>
{
    public static ArgumentNullExceptionBinarySerializer Instance { get; } = new();

    protected override ArgumentNullException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        var paramName = context.Deserialize<string>(ref source);
        if (innerException is { })
        {
            return new ArgumentNullException(message, innerException);
        }
        else if (paramName is null)
        {
            return new ArgumentNullException(null, message);
        }
        else
        {
            var trailer = $" (Parameter '{paramName}')";
            var clearMessage = message.Replace(trailer, "");

            return new ArgumentNullException(paramName, clearMessage);
        }
    }

    protected override void SerializeProps(ArgumentNullException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(value.ParamName, writer);
    }
}
