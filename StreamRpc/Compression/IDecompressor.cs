using System.Buffers;

namespace StreamRpc.Compression;

public interface IDecompressor : IDisposable
{
    void Decompress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination);
}
