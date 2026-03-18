using System.Buffers;

namespace Zarn.Compression;

public interface IDecompressor : IDisposable
{
    void Decompress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination);
}
