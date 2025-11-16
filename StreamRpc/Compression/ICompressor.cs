using System.Buffers;

namespace StreamRpc.Compression;

public interface ICompressor : IDisposable
{
    void Compress(ReadOnlySequence<byte> source, IBufferWriter<byte> output);
}
