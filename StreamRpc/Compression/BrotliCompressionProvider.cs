using System.IO.Compression;

namespace StreamRpc.Compression;

public sealed class BrotliCompressionProvider : CompressionProvider
{
    public override string AlgorithmName => "brotli";

    public override BrotliCompressor CreateCompressor(CompressionLevel compressionLevel)
        => new(compressionLevel);

    public override BrotliDecompressor CreateDecompressor() => new();
}
