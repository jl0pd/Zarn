using System.Buffers;
using Zarn.Collections;

namespace Zarn.Tests;

public sealed class BufferWriterTests
{
    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void WriteOver2Chunks(int payloadSize)
    {
        var writer = new ChunkedArrayPoolBufferWriter<byte>(128, 4096);

        var expected = new byte[payloadSize];
        Random.Shared.NextBytes(expected);

        writer.Write(expected);

        var written = writer.GetSequence().ToArray();

        Assert.Equal(expected, written);
    }

    [Fact]
    public void TestResetResetsAllChunks()
    {
        var writer = new ChunkedArrayPoolBufferWriter<byte>(128, 4096);

        writer.Write(new byte[1000]);

        writer.Reset();

        int processed = 0;
        foreach (var chunk in writer)
        {
            processed++;
            Assert.Equal(0, chunk.Written);
            Assert.Equal(0, chunk.Start);
            Assert.Empty(chunk.WrittenMemory.ToArray());
            Assert.Empty(chunk.WrittenSpan.ToArray());
            Assert.Empty(chunk.Memory.ToArray());
        }

        Assert.NotEqual(0, processed);
    }
}
