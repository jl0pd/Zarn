using System.IO.Compression;

namespace StreamRpc.Compression;

public abstract class CompressionProvider
{
    /// <summary>
    /// Compression algorithm name which is used during handshake.
    /// </summary>
    public abstract string AlgorithmName { get; }

    public abstract ICompressor CreateCompressor(CompressionLevel compressionLevel);

    public abstract IDecompressor CreateDecompressor();
}
