using System.Buffers;

namespace StreamRpc.Protocol;

internal static class ChunkedArrayPoolBufferWriterExtensions
{
    public static SequenceReader<T> GetReader<T>(this ChunkedArrayPoolBufferWriter<T> writer) where T : unmanaged, IEquatable<T>
    {
        return new SequenceReader<T>(writer.GetSequence());
    }
}
