using System.Reflection.Emit;
using Zarn.Invocation;
using Zarn.Utils;

namespace Zarn.TypeGeneration;

internal sealed class CompiledCalleeFactory : ICalleeFactory
{
    private readonly Cache<CalleeBase> _cache;

    public CompiledCalleeFactory(Type implementationType)
    {
        var method = new DynamicMethod("", typeof(CalleeBase), null);

        var il = method.GetILGenerator();
        il.Emit(OpCodes.Newobj, implementationType.GetConstructors().Single());
        il.Emit(OpCodes.Ret);

        var func = method.CreateDelegate<Func<CalleeBase>>();
        _cache = new Cache<CalleeBase>(func);
    }

    public CalleeBase Get() => _cache.Get();

    public void Return(CalleeBase callee) => _cache.Return(callee);
}
