using StreamRpc.Protocol;

namespace StreamRpc.TypeGeneration;

internal interface ICalleeFactory
{
    CalleeBase Get();

    void Return(CalleeBase callee);
}
