using System.Buffers;

namespace StreamRpc.Serialization;

public struct ReadOnlySequenceReader<T>(ReadOnlySequence<T> sequence)
{
    public ReadOnlySequence<T> Remaining { get; private set; } = sequence;

    public readonly ReadOnlySpan<T> FirstSpan => Remaining.FirstSpan;

    public void Advance(int count)
    {
        Remaining = Remaining.Slice(count);
        if (Remaining.Length == 0)
        {
            Remaining = default;
        }
    }
}
