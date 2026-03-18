using System.Buffers;
using System.Diagnostics;

namespace Zarn.Serialization.Serializers.Exceptions;

internal sealed class UnreachableExceptionBinarySerializer : ExceptionSerializerBase<UnreachableException>
{
    public static UnreachableExceptionBinarySerializer Instance { get; } = new();

    protected override UnreachableException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new UnreachableException(message, innerException);
    }

    protected override void SerializeProps(UnreachableException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
