using System.Buffers;
using System.Diagnostics;

namespace Zarn.Tests.Utils;

internal static class SequenceHelper
{
    public static ReadOnlySequence<T> Split<T>(T[] source, int maxChunkSize)
    {
        return Split((ReadOnlyMemory<T>)source, maxChunkSize);
    }

    public static ReadOnlySequence<T> Split<T>(ReadOnlyMemory<T> source, int maxChunkSize)
    {
        if (source.Length == 0)
        {
            return ReadOnlySequence<T>.Empty;
        }

        var sizes = new List<int>();
        int current = 0;

        while (current < source.Length)
        {
            int consumed = Math.Min(maxChunkSize, source.Length - current);
            sizes.Add(consumed);
            current += consumed;
        }

        return Split(source, sizes.ToArray());
    }

    public static ReadOnlySequence<T> Split<T>(ReadOnlyMemory<T> source, int[] sizes)
    {
        Debug.Assert(sizes.Sum() == source.Length);

        if (source.Length == 0)
        {
            return ReadOnlySequence<T>.Empty;
        }

        var first = new Buffer<T>() { Memory = source[..sizes[0]] };
        var current = first;
        int runningIndex = sizes[0];

        for (int i = 1; i < sizes.Length; i++)
        {
            var prev = current;
            current = new Buffer<T>();
            prev.Next = current;
            current.RunningIndex = runningIndex;
            current.Memory = source.Slice(runningIndex, sizes[i]);
            runningIndex += sizes[i];
        }

        return new ReadOnlySequence<T>(first, 0, current, sizes[^1]);
    }

    private sealed class Buffer<T> : ReadOnlySequenceSegment<T>
    {
        public new long RunningIndex
        {
            get => base.RunningIndex;
            set => base.RunningIndex = value;
        }

        public new Buffer<T>? Next
        {
            get => (Buffer<T>?)base.Next;
            set => base.Next = value;
        }

        public new ReadOnlyMemory<T> Memory
        {
            get => base.Memory;
            set => base.Memory = value;
        }
    }
}
