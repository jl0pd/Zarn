namespace StreamRpc.Serialization;

internal ref struct SpanReader<T>(ReadOnlySpan<T> source)
{
    public ReadOnlySpan<T> Remaining { get; private set; } = source;

    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(count, Remaining.Length);

        Remaining = Remaining[count..];
    }
}

internal static class SpanReaderExtensions
{
    public static ReadOnlySpan<T> Scan<T>(this ref SpanReader<T> reader, Func<T, bool> matches)
    {
        var src = reader.Remaining;

        for (int i = 0; i < src.Length; i++)
        {
            if (!matches(src[i]))
            {
                reader.Advance(i);
                return src[..i];
            }
        }

        reader.Advance(reader.Remaining.Length);
        return src;
    }

    public static ReadOnlySpan<char> ScanDigits(this ref SpanReader<char> reader)
    {
        return reader.Scan(char.IsAsciiDigit);
    }

    public static void Expect<T>(this ref SpanReader<T> reader, T value) where T : IEquatable<T>
    {
        if (reader.Remaining.Length == 0)
        {
            throw new InvalidOperationException("Empty reader");
        }

        if (!EqualityComparer<T>.Default.Equals(reader.Remaining[0],  value))
        {
            throw new InvalidOperationException("Value doesn't match");
        }

        reader.Advance(1);
    }
}
