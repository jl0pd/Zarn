using System.Text;
using Zarn.Serialization;

namespace Zarn.Tests;

public class PackedIntTests
{
    [Theory]
    [InlineData(0, "0000.0000")]
    [InlineData(1, "0000.0010")]
    [InlineData(-1, "0000.0001")]
    [InlineData(63, "0111.1110")]
    [InlineData(-63, "0111.1101")]
    [InlineData(-64, "0111.1111")]
    [InlineData(64, "1000.0000 0000.0010")]
    [InlineData(4660, "1010.1000 1001.0001")]
    [InlineData(-4660, "1010.0111 1001.0001")]
    [InlineData(8191, "1011.1110 1111.1111")]
    [InlineData(-8191, "1011.1101 1111.1111")]
    [InlineData(-8192, "1011.1111 1111.1111")]
    [InlineData(8192, "1100.0000 0000.0000 0000.0010")]
    [InlineData(-100_000, "1101.1111 0110.1001 0001.1000")]
    [InlineData(1_000_000, "1100.0000 0010.0100 1111.0100")]
    [InlineData(1_048_576, "1110.0000 0000.0000 0000.0000 0000.0010")]
    [InlineData(16_000_000, "1110.0000 1000.0000 1000.0100 0001.1110")]
    [InlineData(-16_000_000, "1110.1111 0111.1111 1000.0100 0001.1110")]
    [InlineData(268_435_455, "1111.0110 1111.1111 1111.1111 1111.1111 0000.0011")]
    [InlineData(-268_435_456, "1111.0111 1111.1111 1111.1111 1111.1111 0000.0011")]
    [InlineData(1_000_000_000, "1111.0000 1000.0000 1011.0010 1110.0110 0000.1110")]
    [InlineData(34_359_738_368, "1111.1000 0000.0000 0000.0000 0000.0000 0000.0000 0000.0100")]
    [InlineData(-34_359_738_368, "1111.1011 1111.1111 1111.1111 1111.1111 1111.1111 0000.0011")]
    [InlineData(4_398_046_511_104, "1111.1100 0000.0000 0000.0000 0000.0000 0000.0000 0000.0000 0000.0100")]
    [InlineData(-4_398_046_511_104, "1111.1101 1111.1111 1111.1111 1111.1111 1111.1111 1111.1111 0000.0011")]
    [InlineData(36_028_797_018_963_967, "1111.1110 1111.1110 1111.1111 1111.1111 1111.1111 1111.1111 1111.1111 1111.1111")]
    [InlineData(-36_028_797_018_963_968, "1111.1110 1111.1111 1111.1111 1111.1111 1111.1111 1111.1111 1111.1111 1111.1111")]
    [InlineData(72_057_594_037_927_936, "1111.1111 0000.0000 0000.0000 0000.0000 0000.0000 0000.0000 0000.0000 0000.0000 0000.0010")]
    [InlineData(639_304_434_880_000_000, "1111.1111 0000.0000 0110.0000 1011.0101 0010.0000 1111.1110 1000.0111 1011.1110 0001.0001")]
    [InlineData(long.MaxValue, "1111.1111 1111.1110 1111.1111 1111.1111 1111.1111 1111.1111 1111.1111 1111.1111 1111.1111")]
    [InlineData(long.MinValue, "1111.1111 1111.1111 1111.1111 1111.1111 1111.1111 1111.1111 1111.1111 1111.1111 1111.1111")]
    [InlineData(-long.MaxValue, "1111.1111 1111.1101 1111.1111 1111.1111 1111.1111 1111.1111 1111.1111 1111.1111 1111.1111")]
    public void TestNew(long value, string bits)
    {
        Span<byte> bytes = stackalloc byte[PackedInt.MaxSize];

        int bytesWritten = PackedInt.Write(value, bytes);

        if (bits.Length > 0)
        {
            Assert.Equal(bits, BytesToBits(bytes[..bytesWritten]));
        }

        long readValue = PackedInt.Read(bytes, out int bytesRead);

        Assert.Equal(value, readValue);
        Assert.Equal(bytesWritten, bytesRead);
    }

    [Theory]
    [InlineData(0x00, 1)]
    [InlineData(0x80, 2)]
    [InlineData(0xC0, 3)]
    [InlineData(0xE0, 4)]
    [InlineData(0xF0, 5)]
    [InlineData(0xF8, 6)]
    [InlineData(0xFC, 7)]
    [InlineData(0xFE, 8)]
    [InlineData(0xFF, 9)]
    public void TestSize(byte bits, int bytes)
    {
        var actual = PackedInt.GetConsumedBytes(bits);
        Assert.Equal(bytes, actual);
    }

    private static string BytesToBits(ReadOnlySpan<byte> bytes)
    {
        var sb = new StringBuilder(bytes.Length * 10);

        bool first = true;
        foreach (var b in bytes)
        {
            if (!first)
            {
                sb.Append(' ');
            }
            first = false;

            sb.Append(string.Create(9, b, (span, b) =>
            {
                span[0] = ((b >> 7) & 0x01) == 0 ? '0' : '1';
                span[1] = ((b >> 6) & 0x01) == 0 ? '0' : '1';
                span[2] = ((b >> 5) & 0x01) == 0 ? '0' : '1';
                span[3] = ((b >> 4) & 0x01) == 0 ? '0' : '1';
                span[4] = '.';
                span[5] = ((b >> 3) & 0x01) == 0 ? '0' : '1';
                span[6] = ((b >> 2) & 0x01) == 0 ? '0' : '1';
                span[7] = ((b >> 1) & 0x01) == 0 ? '0' : '1';
                span[8] = ((b >> 0) & 0x01) == 0 ? '0' : '1';
            }));
        }

        return sb.ToString();
    }
}
