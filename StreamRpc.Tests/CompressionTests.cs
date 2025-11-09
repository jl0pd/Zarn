using System.Buffers;
using System.IO.Compression;
using System.Text;
using StreamRpc.Compression;
using StreamRpc.Tests.Utils;

namespace StreamRpc.Tests;

public sealed class CompressionTests
{
    private static CompressionLevel[] s_levels =
    [
        CompressionLevel.Optimal,
        CompressionLevel.Fastest,
        CompressionLevel.NoCompression,
        CompressionLevel.SmallestSize,
    ];

    [Theory]
    [InlineData("")]
    [InlineData("some text")]
    [InlineData("another text")]
    public void Roundtrip(string text)
    {
        var provider = (CompressionProvider)new BrotliCompressionProvider();

        var utf8Bytes = Encoding.UTF8.GetBytes(text);
        var decompressor = provider.CreateDecompressor();
        foreach (var level in s_levels)
        {
            var compressor = provider.CreateCompressor(level);

            for (int i = 1; i < utf8Bytes.Length; i++)
            {
                AssertRoundtrip(SequenceHelper.Split(utf8Bytes, i), compressor, decompressor);
            }
        }
    }

    private static void AssertRoundtrip(ReadOnlySequence<byte> bytes, ICompressor compressor, IDecompressor decompressor)
    {
        var writer = new ArrayBufferWriter<byte>((int)bytes.Length);
        compressor.Compress(bytes, writer);

        for (int i = 1; i < writer.WrittenCount; i++)
        {
            var decompressionSource = SequenceHelper.Split(writer.WrittenMemory, i);
            var decompressed = new ArrayBufferWriter<byte>((int)bytes.Length);

            decompressor.Decompress(decompressionSource, decompressed);

            Assert.Equal(bytes.ToArray(), decompressed.WrittenSpan.ToArray());
        }
    }
}
