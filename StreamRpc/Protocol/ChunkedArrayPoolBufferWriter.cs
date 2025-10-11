using System.Buffers;
using System.Collections;
using System.Diagnostics;
using System.Numerics;
using StreamRpc.Serialization;
using StreamRpc.Utils;

namespace StreamRpc.Protocol;

internal sealed class BufferSegment<T> : ReadOnlySequenceSegment<T>
{
    public void SetRunningIndex(long index)
    {
        RunningIndex = index;
    }

    public void SetNext(BufferSegment<T>? segment)
    {
        Next = segment;
    }

    public void SetMemory(ReadOnlyMemory<T> memory)
    {
        Memory = memory;
    }
}

internal static class BufferSegmentPool<T>
{
    private static readonly Cache<BufferSegment<T>> s_segments
        = new(Environment.ProcessorCount * 2, () => new BufferSegment<T>());

    public static BufferSegment<T> Rent()
    {
        return s_segments.Get();
    }

    public static void Return(BufferSegment<T> segment)
    {
        segment.SetNext(null);
        segment.SetMemory(Memory<T>.Empty);
        s_segments.Return(segment);
    }
}

internal sealed class ChunkedArrayPoolBufferWriter<T>(int minAllocationSize, int maxPoolSize) : IBufferWriter<T>
{
    public struct Chunk
    {
        public T[] Array;
        public int Written;

        public readonly Span<T> WritableSpan => Array.AsSpan(Written);
        public readonly Memory<T> WritableMemory => Array.AsMemory(Written);

        public readonly ReadOnlySpan<T> WrittenSpan => Array.AsSpan(0, Written);
        public readonly ReadOnlyMemory<T> WrittenMemory => Array.AsMemory(0, Written);

        public readonly int Free => Array.Length - Written;
    }

    public ReadOnlySequence<T> GetSequence()
    {
        if (_chunks[_currentChunk].Array is null)
        {
            return ReadOnlySequence<T>.Empty;
        }

        BufferSegment<T>? firstSegment = null;
        BufferSegment<T>? current = null;
        long runningIndex = 0;

        foreach (var chunk in this)
        {
            var newChunk = BufferSegmentPool<T>.Rent();
            newChunk.SetMemory(chunk.WrittenMemory);
            newChunk.SetRunningIndex(runningIndex);
            current?.SetNext(newChunk);
            runningIndex += chunk.Written;

            firstSegment ??= newChunk;
            current = newChunk;
        }

        var result = new ReadOnlySequence<T>(firstSegment, 0, current, current.Memory.Length);
        Debug.Assert(result.Length == TotalLength);
        return result;
    }

    public ReadOnlySequenceReader<T> GetReader() => new ReadOnlySequenceReader<T>(GetSequence());

    private Chunk[] _chunks = new Chunk[1];
    private int _currentChunk;

    public Chunk FirstChunk => _chunks[0];

    public long TotalLength
    {
        get
        {
            long length = 0;
            foreach (var chunk in this)
            {
                length += chunk.Written;
            }

            return length;
        }
    }

    public void Reset()
    {
        foreach (ref var chunk in _chunks.AsSpan())
        {
            if (chunk.Array is { } ar)
            {
                if (ar.Length < maxPoolSize)
                {
                    ArrayPool<T>.Shared.Return(chunk.Array);
                }

                chunk.Array = default!;
            }

            chunk.Written = 0;
        }

        _currentChunk = 0;
    }

    public void Advance(int count)
    {
        ref var chunk = ref _chunks[_currentChunk];
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, chunk.Free);
        chunk.Written += count;
        Debug.Assert(chunk.Written >= 0);
        if (chunk.Free == 0)
        {
            _currentChunk++;
            EnsureCapacity();
        }
    }

    private void EnsureCapacity()
    {
        if (_currentChunk == _chunks.Length)
        {
            var newChunks = new Chunk[BitOperations.RoundUpToPowerOf2((uint)_currentChunk)];
            _chunks.CopyTo(newChunks, 0);
            _chunks = newChunks;
        }
    }

    public Memory<T> GetMemory(int sizeHint = 0)
    {
        return GetChunk(sizeHint).WritableMemory;
    }

    public Span<T> GetSpan(int sizeHint = 0)
    {
        return GetChunk(sizeHint).WritableSpan;
    }

    private ref Chunk GetChunk(int sizeHint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

        ref var currentChunk = ref _chunks[_currentChunk];
        if (currentChunk.Array is null)
        {
            if (sizeHint >= maxPoolSize)
            {
                currentChunk.Array = new T[sizeHint];
            }
            else
            {
                currentChunk.Array = ArrayPool<T>.Shared.Rent(Math.Max(minAllocationSize, sizeHint));
            }

            return ref currentChunk;
        }

        if (sizeHint <= currentChunk.Free)
        {
            return ref currentChunk;
        }

        _currentChunk++;
        EnsureCapacity();
        return ref GetChunk(Math.Max(sizeHint, minAllocationSize));
    }

    public Enumerator GetEnumerator() => new(this);

    public struct Enumerator(ChunkedArrayPoolBufferWriter<T> writer) : IEnumerator<Chunk>
    {
        private int _index = -1;

        readonly object IEnumerator.Current => Current;

        public readonly Chunk Current => writer._chunks[_index];

        public bool MoveNext()
        {
            if (_index < writer._currentChunk)
            {
                _index++;
                return true;
            }

            return false;
        }

        public void Reset() => _index = -1;

        public void Dispose() => writer = default!;
    }
}
