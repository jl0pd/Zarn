namespace Zarn.Compression;

/// <summary>
/// Base type for providing compression support during RPC communication.
/// </summary>
public abstract class CompressionProvider
{
    /// <summary>
    /// Compression algorithm name which is used during handshake.
    /// </summary>
    public abstract string AlgorithmName { get; }

    public abstract ICompressor CreateCompressor();

    public abstract IDecompressor CreateDecompressor();
}
