using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Exceptions;

internal sealed class FileNotFoundExceptionBinarySerializer : ExceptionSerializerBase<FileNotFoundException>
{
    public static FileNotFoundExceptionBinarySerializer Instance { get; } = new();

    protected override FileNotFoundException DeserializeCore(string message, Exception? innerException, ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        var fileName = context.Deserialize<string>(ref source);
        return new FileNotFoundException(message, fileName, innerException);
    }

    protected override void SerializeProps(FileNotFoundException value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(value.FileName, writer);
    }
}
