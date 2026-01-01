using System.Buffers;

namespace StreamRpc.Protocol;

internal abstract class GenericMethodInvokeTrampoline
{
    public abstract void Invoke(CalleeBase callee, ref SequenceReader<byte> argumentsReader);

    public abstract bool Matches(ReadOnlySpan<Type> genericArgs);
}
