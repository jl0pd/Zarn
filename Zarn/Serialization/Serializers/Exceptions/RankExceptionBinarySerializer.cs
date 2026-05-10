using System.Buffers;

namespace Zarn.Serialization.Serializers.Exceptions;

internal sealed class RankExceptionBinarySerializer : ExceptionSerializerBase<RankException>
{
    public static RankExceptionBinarySerializer Instance { get; } = new();

    protected override RankException DeserializeCore(string message, Exception? innerException, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new RankException(message, innerException);
    }

    protected override void SerializeProps(RankException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
