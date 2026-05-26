using System.Buffers;
using System.IO.Compression;
using System.Text;
using Zarn.Compression;
using Zarn.Tests.Utils;

namespace Zarn.Tests;

public sealed class CompressionTests
{
    private static readonly CompressionLevel[] s_levels =
    [
        CompressionLevel.Optimal,
        CompressionLevel.Fastest,
        CompressionLevel.NoCompression,
        CompressionLevel.SmallestSize,
    ];

    public static IEnumerable<TheoryDataRow<string>> GetLongStrings()
    {
        return
        [
            new TheoryDataRow<string>(new string('a', 1024)),
            new TheoryDataRow<string>(string.Join("", Enumerable.Range(0, 1024).Select(x => (byte)Random.Shared.Next(0, 255)))),
        ];
    }

    [Theory]
    [InlineData("")]
    [InlineData("some text")]
    [InlineData("another text")]
    [MemberData(nameof(GetLongStrings))]
    public void Roundtrip(string text)
    {
        foreach (var level in s_levels)
        {
            var provider = (CompressionProvider)new BrotliCompressionProvider(level);

            var utf8Bytes = Encoding.UTF8.GetBytes(text);
            var decompressor = provider.CreateDecompressor();
            var compressor = provider.CreateCompressor();

            for (int i = 1; i < utf8Bytes.Length; i++)
            {
                AssertRoundtrip(SequenceHelper.Split(utf8Bytes, i), compressor, decompressor);
            }
        }
    }

    private static void AssertRoundtrip(ReadOnlySequence<byte> bytes, ICompressor compressor, IDecompressor decompressor)
    {
        var writer = new ArrayBufferWriter<byte>(16);
        compressor.Compress(bytes, writer);

        for (int i = 1; i < writer.WrittenCount; i++)
        {
            var decompressionSource = SequenceHelper.Split(writer.WrittenMemory, i);
            var decompressed = new ArrayBufferWriter<byte>(16);

            decompressor.Decompress(decompressionSource, decompressed);

            Assert.Equal(bytes.ToArray(), decompressed.WrittenSpan.ToArray());
        }
    }
}
