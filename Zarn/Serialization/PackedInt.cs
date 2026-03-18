using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Zarn.Serialization;

/// <summary>
/// Serializes given <see cref="long"/> trying to save as much bytes as possible.
/// Length is written using 1-based encoding and terminated with 0 bit.
/// First bit after length is sign - 0 is positive, 1 is negative.
/// <list type="bullet">
/// <item>0 - 6 bits, 1 total bytes</item>
/// <item>10 - 13 bits, 2 total bytes</item>
/// <item>110 - 28 bits, 4 total bytes</item>
/// <item>1110 - 59 bits, 8 total bytes</item>
/// <item>11110 - 64 bits, 9 total bytes</item>
/// </list>
/// </summary>
internal static class PackedInt
{
    public const int MaxSize = 9;

    public static int GetRequiredSize(long value)
    {
        if (value < 0)
        {
            value = -value;
        }

        int significantBits = 64 - (int)long.LeadingZeroCount(value);
        if (significantBits <= 6)
        {
            return 1;
        }
        else if (significantBits <= 13)
        {
            return 2;
        }
        else if (significantBits <= 28)
        {
            return 4;
        }
        else if (significantBits <= 59)
        {
            return 8;
        }
        else
        {
            return 9;
        }
    }

    // TODO: bad method name
    public static int GetConsumedBytes(byte firstByte)
    {
        int bytes = (int)Math.Pow(2, uint.LeadingZeroCount(~((uint)firstByte << 24)));
        if (bytes > 9)
        {
            bytes = 9;
        }

        return bytes;
    }

    public static bool TryRead(ReadOnlySpan<byte> span, out long value, out int consumed)
    {
        Unsafe.SkipInit(out value);
        int isNegative;
        consumed = (int)Math.Pow(2, uint.LeadingZeroCount(~((uint)span[0] << 24)));
        switch (consumed)
        {
            case 1:
                value = span[0] & 0x3F;
                isNegative = span[0] >> 6;
                break;
            case 2:
                if (span.Length < 2)
                {
                    return false;
                }
                value = BinaryPrimitives.ReadUInt16BigEndian(span) & 0x1FFFu;
                isNegative = (span[0] >> 5) & 1;
                break;
            case 4:
                if (span.Length < 4)
                {
                    return false;
                }
                value = BinaryPrimitives.ReadUInt32BigEndian(span) & 0x0FFFFFFFu;
                isNegative = (span[0] >> 4) & 1;
                break;
            case 8:
                if (span.Length < 8)
                {
                    return false;
                }
                value = (long)(BinaryPrimitives.ReadUInt64BigEndian(span) & 0x07FFFFFF_FFFFFFFFu);
                isNegative = (span[0] >> 3) & 1;
                break;
            case 16:
                if (span.Length < 9)
                {
                    return false;
                }
                value = BinaryPrimitives.ReadInt64BigEndian(span[1..]);
                isNegative = (span[0] >> 2) & 1;
                consumed = 9;
                break;
            default:
                throw new InvalidDataException("Invalid length");
        }

        if (isNegative == 1)
        {
            value = -value;
        }

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
        int isNegative = 0;
        if (value < 0)
        {
            value = -value;
            isNegative = 1;
        }

        int significantBits = 64 - (int)long.LeadingZeroCount(value);
        if (significantBits <= 6)
        {
            destination[0] = (byte)((isNegative << 6) | (int)value);
            return 1;
        }
        else if (significantBits <= 13)
        {
            BinaryPrimitives.WriteUInt16BigEndian(destination, (ushort)value);
            destination[0] |= (byte)(0x80 | (isNegative << 5));
            return 2;
        }
        else if (significantBits <= 28)
        {
            BinaryPrimitives.WriteUInt32BigEndian(destination, (uint)value);
            destination[0] |= (byte)(0xC0 | (isNegative << 4));
            return 4;
        }
        else if (significantBits <= 59)
        {
            BinaryPrimitives.WriteUInt64BigEndian(destination, (ulong)value);
            destination[0] |= (byte)(0xE0 | (isNegative << 3));
            return 8;
        }
        else
        {
            BinaryPrimitives.WriteUInt64BigEndian(destination[1..], (ulong)value);
            destination[0] |= (byte)(0xF0 | (isNegative << 2));
            return 9;
        }
    }
}
