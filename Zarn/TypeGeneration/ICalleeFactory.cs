using Zarn.Invocation;

namespace Zarn.TypeGeneration;

internal interface ICalleeFactory : IDisposable
{
    CalleeBase Get();

    void Return(CalleeBase callee);
}
