using System.Buffers;
using System.Collections;
using System.Diagnostics;

namespace Zarn.Collections;

internal sealed class ChunkedArrayPoolBufferWriter<T>(int minAllocationSize, int maxPoolSize) : IBufferWriter<T>
{
    public sealed class Chunk : ReadOnlySequenceSegment<T>
    {
        public T[] Array = [];
        public int Start;
        public int Written;
        public required int ChunkIndex;

        public Span<T> WritableSpan => Array.AsSpan(Start + Written);
        public Memory<T> WritableMemory => Array.AsMemory(Start + Written);

        public ReadOnlySpan<T> WrittenSpan => Array.AsSpan(Start, Written);
        public ReadOnlyMemory<T> WrittenMemory => Array.AsMemory(Start, Written);

        public int Free => Array.Length - Written - Start;

        public new Chunk? Next
        {
            get => (Chunk?)base.Next;
            set => base.Next = value;
        }

        public void SetMemory() => Memory = WrittenMemory;

        public new long RunningIndex
        {
            get => base.RunningIndex;
            set => base.RunningIndex = value;
        }
    }

    public ReadOnlySequence<T> GetSequence()
    {
        if (FirstChunk is null || FirstChunk.Written == 0)
        {
            return default;
        }

        Debug.Assert(LastChunk is { });

        long runningIndex = 0;
        for (var current = FirstChunk; current is { }; current = current.Next)
        {
            current.SetMemory();
            current.RunningIndex = runningIndex;
            runningIndex += current.Written;
        }

        return new ReadOnlySequence<T>(FirstChunk, 0, LastChunk, LastChunk.Written);
    }

    public Chunk FirstChunkRequired => FirstChunk ?? throw ThrowHelper.Unreachable;

    public Chunk? FirstChunk { get; private set; }

    public Chunk? LastChunk { get; private set; }

    public bool IsSingleChunk => FirstChunkRequired == LastChunk;

    public long TotalLength
    {
        get
        {
            long length = 0;
            for (var current = FirstChunk; current is { }; current = current.Next)
            {
                length += current.Written;
            }
            return length;
        }
    }

    public void Reset()
    {
        foreach (var chunk in this)
        {
            if (chunk.Array.Length <= maxPoolSize)
            {
                ArrayPool<T>.Shared.Return(chunk.Array);
            }

            chunk.Array = [];
            chunk.Written = 0;
            chunk.Start = 0;
        }

        LastChunk = null;
    }

    public void Advance(int count)
    {
        var chunk = LastChunk ?? throw new InvalidOperationException();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, chunk.Free);
        chunk.Written += count;
    }

    public Memory<T> GetMemory(int sizeHint = 0)
    {
        return GetChunk(sizeHint).WritableMemory;
    }

    public Span<T> GetSpan(int sizeHint = 0)
    {
        return GetChunk(sizeHint).WritableSpan;
    }

    private Chunk GetChunk(int sizeHint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);

        var currentChunk = LastChunk;
        if (currentChunk is null)
        {
            FirstChunk = LastChunk = currentChunk = FirstChunk ?? new Chunk() { ChunkIndex = 0 };
            if (sizeHint > maxPoolSize)
            {
                currentChunk.Array = new T[sizeHint];
            }
            else
            {
                currentChunk.Array = ArrayPool<T>.Shared.Rent(Math.Max(minAllocationSize, sizeHint));
            }
            return currentChunk;
        }

        if (currentChunk.Free > 0)
        {
            if (sizeHint == 0)
            {
                return currentChunk;
            }
            if (sizeHint < currentChunk.Free)
            {
                return currentChunk;
            }
        }

        var newChunk = currentChunk.Next ?? new Chunk() { ChunkIndex = currentChunk.ChunkIndex + 1 };
        currentChunk.Next = newChunk;
        if (sizeHint > maxPoolSize)
        {
            newChunk.Array = new T[sizeHint];
        }
        else
        {
            newChunk.Array = ArrayPool<T>.Shared.Rent(Math.Max(minAllocationSize, sizeHint));
        }
        LastChunk = newChunk;

        return newChunk;
    }

    public Enumerator GetEnumerator() => new(this);

    public struct Enumerator(ChunkedArrayPoolBufferWriter<T>? writer) : IEnumerator<Chunk>
    {
        private Chunk? _current;

        readonly object IEnumerator.Current => Current;

        public readonly Chunk Current => _current ?? throw new InvalidOperationException();

        public bool MoveNext()
        {
            if (writer is null)
            {
                throw new ObjectDisposedException(nameof(Enumerator));
            }

            if (_current is null)
            {
                _current = writer.FirstChunk;
            }
            else if (_current.Next is null)
            {
                return false;
            }

            return _current is { };
        }

        public void Reset() => _current = null;

        public void Dispose()
        {
            _current = null;
            writer = null;
        }
    }
}
