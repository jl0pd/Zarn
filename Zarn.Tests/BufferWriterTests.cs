using System.Buffers;
using Zarn.Collections;

namespace Zarn.Tests;

public sealed class BufferWriterTests
{
    [Fact]
    public void WriteOver2Chunks()
    {
        var writer = new ChunkedArrayPoolBufferWriter<byte>(128, 4096);

        var expected = new byte[307];
        Random.Shared.NextBytes(expected);

        writer.Write(expected);

        var written = writer.GetSequence().ToArray();

        Assert.Equal(expected, written);
    }
}
