using Zarn.Invocation;

namespace Zarn.TypeGeneration;

internal sealed class UnreachableCalleeFactory : ICalleeFactory
{
    public static UnreachableCalleeFactory Instance { get; } = new();

    public CalleeBase Get() => throw ThrowHelper.Unreachable;

    public void Return(CalleeBase callee) => throw ThrowHelper.Unreachable;
}
