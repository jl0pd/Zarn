using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Zarn.Serialization;

/// <summary>
/// Serializes given <see cref="long"/> trying to save as much bytes as possible.
/// Value is zigzag-encoded so sign doesn't occupy a dedicated bit and small negative values stay small.
/// Length is stored using 1-based encoding. Multi-byte payloads are little-endian.
/// <list type="bullet">
/// <item>0 - 7 bits, 1 total bytes</item>
/// <item>10 - 14 bits, 2 total bytes</item>
/// <item>110 - 21 bits, 3 total bytes</item>
/// <item>1110 - 28 bits, 4 total bytes</item>
/// <item>11110 - 35 bits, 5 total bytes</item>
/// <item>111110 - 42 bits, 6 total bytes</item>
/// <item>1111110 - 49 bits, 7 total bytes</item>
/// <item>11111110 - 56 bits, 8 total bytes</item>
/// <item>11111111 - 64 bits, 9 total bytes</item>
/// </list>
/// </summary>
internal static class PackedInt
{
    public const int MaxSize = 9;

    private static ReadOnlySpan<byte> SizeBySignificantBits => [1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 9];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SizeFromSignificantBits(int significantBits) 
        => Unsafe.Add(ref MemoryMarshal.GetReference(SizeBySignificantBits), (significantBits + 6) / 7);

    public static int GetRequiredSize(long value)
    {
        ulong zigZag = ((ulong)value << 1) ^ (ulong)(value >> 63);
        int significantBits = 64 - (int)ulong.LeadingZeroCount(zigZag);
        return SizeFromSignificantBits(significantBits);
    }

    public static int GetConsumedBytes(byte firstByte)
    {
        return (int)uint.LeadingZeroCount(~((uint)firstByte << 24)) + 1;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="span"></param>
    /// <param name="value"></param>
    /// <param name="consumed">Contains amount of bytes consumed when method returns <see langword="true"/>,
    /// otherwise returns amount of bytes required</param>
    /// <returns></returns>
    public static bool TryRead(ReadOnlySpan<byte> span, out long value, out int consumed)
    {
        Unsafe.SkipInit(out value);
        consumed = GetConsumedBytes(span[0]);
        ulong zigZag;
        switch (consumed)
        {
            case 1:
                zigZag = span[0] & 0x7Fu;
                break;
            case 2:
                if (span.Length < 2)
                {
                    return false;
                }
                ulong header16 = BinaryPrimitives.ReadUInt16LittleEndian(span);
                zigZag = (header16 >> 8 << 6) | (header16 & 0x3Fu);
                break;
            case 3:
                if (span.Length < 3)
                {
                    return false;
                }
                ulong header24 = BinaryPrimitives.ReadUInt16LittleEndian(span[1..]);
                zigZag = (header24 << 5) | (span[0] & 0x1Fu);
                break;
            case 4:
                if (span.Length < 4)
                {
                    return false;
                }
                ulong header28 = BinaryPrimitives.ReadUInt16LittleEndian(span[1..]) | ((uint)span[3] << 16);
                zigZag = (header28 << 4) | (span[0] & 0xFu);
                break;
            case 5:
                if (span.Length < 5)
                {
                    return false;
                }
                ulong header40 = BinaryPrimitives.ReadUInt32LittleEndian(span[1..]);
                zigZag = (header40 << 3) | (span[0] & 7u);
                break;
            case 6:
                if (span.Length < 6)
                {
                    return false;
                }
                ulong header48 = BinaryPrimitives.ReadUInt32LittleEndian(span[1..]) | ((ulong)span[5] << 32);
                zigZag = (header48 << 2) | (span[0] & 3u);
                break;
            case 7:
                if (span.Length < 7)
                {
                    return false;
                }
                ulong header56 = BinaryPrimitives.ReadUInt32LittleEndian(span[1..])
                               | ((ulong)BinaryPrimitives.ReadUInt16LittleEndian(span[5..]) << 32);
                zigZag = (header56 << 1) | (span[0] & 1u);
                break;
            case 8:
                if (span.Length < 8)
                {
                    return false;
                }
                zigZag = BinaryPrimitives.ReadUInt64LittleEndian(span) >> 8;
                break;
            case 9:
                if (span.Length < 9)
                {
                    return false;
                }
                zigZag = BinaryPrimitives.ReadUInt64LittleEndian(span[1..]);
                break;
            default:
                throw ThrowHelper.Unreachable;
        }

        value = (long)((zigZag >> 1) ^ (0ul - (zigZag & 1ul)));
        return true;
    }

    public static long Read(ReadOnlySpan<byte> span, out int consumed)
    {
        if (!TryRead(span, out long value, out consumed))
        {
            throw new ArgumentException("Span is too short", nameof(span));
        }

        return value;
    }

    public static int Write(long value, Span<byte> destination)
    {
        ulong zigZag = ((ulong)value << 1) ^ (ulong)(value >> 63);

        int significantBits = 64 - (int)ulong.LeadingZeroCount(zigZag);
        switch (SizeFromSignificantBits(significantBits))
        {
            case 1:
                destination[0] = (byte)zigZag;
                return 1;
            case 2:
                BinaryPrimitives.WriteUInt16LittleEndian(destination, (ushort)((((zigZag >> 6) << 8) | 0x80ul) | (zigZag & 0x3Ful)));
                return 2;
            case 3:
                BinaryPrimitives.WriteUInt16LittleEndian(destination[1..], (ushort)(zigZag >> 5));
                destination[0] = (byte)(0xC0 | (zigZag & 0x1Ful));
                return 3;
            case 4:
                BinaryPrimitives.WriteUInt32LittleEndian(destination[1..], (uint)(zigZag >> 4));
                destination[0] = (byte)(0xE0 | (zigZag & 0xFul));
                return 4;
            case 5:
                BinaryPrimitives.WriteUInt32LittleEndian(destination[1..], (uint)(zigZag >> 3));
                destination[0] = (byte)(0xF0 | (zigZag & 7ul));
                return 5;
            case 6:
                destination[5] = (byte)(zigZag >> 34);
                BinaryPrimitives.WriteUInt32LittleEndian(destination[1..], (uint)(zigZag >> 2));
                destination[0] = (byte)(0xF8 | (zigZag & 3ul));
                return 6;
            case 7:
                BinaryPrimitives.WriteUInt16LittleEndian(destination[5..], (ushort)(zigZag >> 33));
                BinaryPrimitives.WriteUInt32LittleEndian(destination[1..], (uint)(zigZag >> 1));
                destination[0] = (byte)(0xFC | (zigZag & 1ul));
                return 7;
            case 8:
                BinaryPrimitives.WriteUInt64LittleEndian(destination, (zigZag << 8) | 0xFEul);
                return 8;
            case 9:
                BinaryPrimitives.WriteUInt64LittleEndian(destination[1..], zigZag);
                destination[0] = 0xFF;
                return 9;
            default:
                throw ThrowHelper.Unreachable;
        }
    }
}
