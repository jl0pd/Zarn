using System.IO.Compression;

namespace StreamRpc.Compression;

public sealed class BrotliCompressionProvider(CompressionLevel compressionLevel) : CompressionProvider
{
    public override string AlgorithmName => "brotli";

    public override BrotliCompressor CreateCompressor() => new(compressionLevel);

    public override BrotliDecompressor CreateDecompressor() => new();
}
