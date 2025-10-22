using System.Buffers;
using System.Runtime.ExceptionServices;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal abstract class ExceptionSerializerBase<T> : BinarySerializer<T> where T : Exception
{
    public sealed override bool CanConvert(Type type)
    {
        return type == typeof(T); // convert exact type, without derived types
    }

    public sealed override T Deserialize(ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        var innerException = (Exception?)context.DeserializeAny(ref source);
        var stackTrace = context.Deserialize<string?>(ref source);
        var message = context.Deserialize<string>(ref source);
        var hresult = context.Deserialize<int>(ref source);

        var result = DeserializeCore(message, innerException, ref source, context);
        if (stackTrace is { })
        {
            ExceptionDispatchInfo.SetRemoteStackTrace(result, stackTrace);
        }

        result.HResult = hresult;

        return result;
    }

    protected abstract T DeserializeCore(string message, Exception? innerException, ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context);

    public sealed override void Serialize(T value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.SerializeAny(value.InnerException, writer);
        context.Serialize(value.StackTrace, writer);
        context.Serialize(value.Message, writer);
        context.Serialize(value.HResult, writer);

        SerializeProps(value, writer, context);
    }

    protected abstract void SerializeProps(T value, IBufferWriter<byte> writer, BinarySerializationContext context);
}
