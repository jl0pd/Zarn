using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class AggregateExceptionBinarySerializer : ExceptionSerializerBase<AggregateException>
{
    public static AggregateExceptionBinarySerializer Instance { get; } = new();

    protected override AggregateException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        var innerExceptions = ObjectArrayBinarySerializer.Instance.Deserialize(ref source, context) ?? throw new InvalidDataException();

        var ex = innerExceptions.Cast<Exception>().ToArray();

        return new AggregateException(message, ex);
    }

    protected override void SerializeProps(AggregateException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        ObjectArrayBinarySerializer.Instance.Serialize(value.InnerExceptions.ToArray(), writer, context);
    }
}
