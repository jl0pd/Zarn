using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

// fund fact: this exception has dedicated OpCode `ckfinite` for raising
internal sealed class NotFiniteNumberExceptionBinarySerializer : ExceptionSerializerBase<NotFiniteNumberException>
{
    public static NotFiniteNumberExceptionBinarySerializer Instance { get; } = new();

    protected override NotFiniteNumberException DeserializeCore(string message, Exception? innerException, ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        var offendingNumber = context.Deserialize<double>(ref source);
        return new NotFiniteNumberException(message, offendingNumber, innerException);
    }

    protected override void SerializeProps(NotFiniteNumberException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(value.OffendingNumber, writer);
    }
}
