using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class DataMisalignedExceptionBinarySerializer : ExceptionSerializerBase<DataMisalignedException>
{
    public static DataMisalignedExceptionBinarySerializer Instance { get; } = new();

    protected override DataMisalignedException DeserializeCore(string message, Exception? innerException, ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        return new DataMisalignedException(message, innerException);
    }

    protected override void SerializeProps(DataMisalignedException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
