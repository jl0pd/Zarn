using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class OperationCanceledExceptionBinarySerializer : ExceptionSerializerBase<OperationCanceledException>
{
    public static OperationCanceledExceptionBinarySerializer Instance { get; } = new();

    protected override OperationCanceledException DeserializeCore(string message, Exception? innerException, ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        return new OperationCanceledException(message, innerException);
    }

    protected override void SerializeProps(OperationCanceledException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        // cannot serialize cancellationToken, because people would definitely rely on CancellationToken to be same instance
        // as on client, but CancellationTokenSource cannot be made same
    }
}
