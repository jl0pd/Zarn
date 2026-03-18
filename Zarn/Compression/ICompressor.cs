using System.Buffers;

namespace Zarn.Compression;

public interface ICompressor : IDisposable
{
    void Compress(ReadOnlySequence<byte> source, IBufferWriter<byte> output);
}
