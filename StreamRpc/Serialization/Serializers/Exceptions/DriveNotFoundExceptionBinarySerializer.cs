using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class DriveNotFoundExceptionBinarySerializer : ExceptionSerializerBase<DriveNotFoundException>
{
    public static DriveNotFoundExceptionBinarySerializer Instance { get; } = new();

    protected override DriveNotFoundException DeserializeCore(string message, Exception? innerException, ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        return new DriveNotFoundException(message, innerException);
    }

    protected override void SerializeProps(DriveNotFoundException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
    }
}
