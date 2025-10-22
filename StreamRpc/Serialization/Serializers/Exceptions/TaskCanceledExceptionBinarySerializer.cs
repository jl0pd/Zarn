using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class TaskCanceledExceptionBinarySerializer : ExceptionSerializerBase<TaskCanceledException>
{
    public static TaskCanceledExceptionBinarySerializer Instance { get; } = new();

    protected override TaskCanceledException DeserializeCore(string message, Exception? innerException, ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        return new TaskCanceledException(message, innerException);
    }

    protected override void SerializeProps(TaskCanceledException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        // cannot serialize TaskCanceledException.Task
    }
}
