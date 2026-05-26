using System.Buffers;
using System.Diagnostics;
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
}
