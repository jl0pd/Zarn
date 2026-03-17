using Zarn.Protocol;

namespace Zarn.TypeGeneration;

internal interface ICalleeFactory
{
    CalleeBase Get();

    void Return(CalleeBase callee);
}
