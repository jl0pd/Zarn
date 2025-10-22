using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class ArgumentOutOfRangeExceptionBinarySerializer : ExceptionSerializerBase<ArgumentOutOfRangeException>
{
    public static ArgumentOutOfRangeExceptionBinarySerializer Instance { get; } = new();

    private static readonly string[] s_newLines = ["\r\n", "\n", "\r"];

    protected override ArgumentOutOfRangeException DeserializeCore(string message, Exception? innerException, ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        var paramName = context.Deserialize<string>(ref source);
        var actualValue = context.DeserializeAny(ref source);

        if (innerException is { })
        {
            return new ArgumentOutOfRangeException(message, innerException);
        }
        else if (paramName is null && actualValue is null)
        {
            return new ArgumentOutOfRangeException(null, message);
        }
        else
        {
            var trailer = $" (Parameter '{paramName}')";
            var clearMessage = message.Replace(trailer, "").Split(s_newLines, StringSplitOptions.None)[0];

            return new ArgumentOutOfRangeException(paramName, actualValue, clearMessage);
        }
    }

    protected override void SerializeProps(ArgumentOutOfRangeException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(value.ParamName, writer);
        context.SerializeAny(value.ActualValue, writer);
    }
}
