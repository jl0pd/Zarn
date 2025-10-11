using System.Buffers;

namespace StreamRpc.Serialization;

public abstract class MemoryProvider
{
    public abstract IMemoryOwner<byte> ToMemory(ReadOnlySequence<byte> source);

    public abstract IMemoryOwner<byte> ToMemory(ReadOnlySpan<byte> source);
}
