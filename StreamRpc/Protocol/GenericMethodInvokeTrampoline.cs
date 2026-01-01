using System.Buffers;

namespace StreamRpc.Protocol;

internal abstract class GenericMethodInvokeTrampoline
{
    public void Invoke(CalleeBase callee, ref SequenceReader<byte> argumentsReader)
    {
        InvokeCore(callee, ref argumentsReader);
    }

    public abstract void InvokeCore(CalleeBase callee, ref SequenceReader<byte> argumentsReader);

    public abstract bool Matches(ReadOnlySpan<Type> genericArgs);
}
