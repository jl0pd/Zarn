using System.Collections.Concurrent;
using System.Reflection.Emit;
using Zarn.TypeGeneration;

namespace Zarn.EnumerableSupport;

internal static class GetEnumeratorCache
{
    private static readonly ConcurrentDictionary<Type, Func<object, object>> s_getEnumerator = [];
    private static readonly ConcurrentDictionary<Type, Func<object, CancellationToken, object>> s_getAsyncEnumerator = [];
    private static readonly EnumeratorCalleeFactory s_enumeratorCalleeFactory = new();

    public static ICalleeFactory GetFactory(Type enumerableGenArg)
    {
        return s_enumeratorCalleeFactory.GetFactory(enumerableGenArg);
    }

    public static object GetEnumerator(object enumerable, Type genArg)
    {
        return s_getEnumerator.GetOrAdd(genArg, CreateInvokeGetEnumerator).Invoke(enumerable);
    }

    public static object GetAsyncEnumerator(object enumerable, Type genArg, CancellationToken cancellationToken)
    {
        return s_getAsyncEnumerator.GetOrAdd(genArg, CreateInvokeGetAsyncEnumerator).Invoke(enumerable, cancellationToken);
    }

    private static Func<object, object> CreateInvokeGetEnumerator(Type genericArg)
    {
        var method = new DynamicMethod("", typeof(object), [typeof(object)]);

        /*
            return ((IEnumerable<T>)arg).GetEnumerator();
        */

        var type = typeof(IEnumerable<>).MakeGenericType(genericArg);

        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, type);
        il.Emit(OpCodes.Callvirt, type.GetMethod(nameof(IEnumerable<int>.GetEnumerator))!);
        il.Emit(OpCodes.Ret);

        return method.CreateDelegate<Func<object, object>>();
    }

    private static Func<object, CancellationToken, object> CreateInvokeGetAsyncEnumerator(Type genericArg)
    {
        var method = new DynamicMethod("", typeof(object), [typeof(object), typeof(CancellationToken)]);

        /*
            return ((IAsyncEnumerable<T>)arg).GetAsyncEnumerator(cancellationToken);
        */

        var type = typeof(IAsyncEnumerable<>).MakeGenericType(genericArg);

        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, type);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Callvirt, type.GetMethod(nameof(IAsyncEnumerable<int>.GetAsyncEnumerator))!);
        il.Emit(OpCodes.Ret);

        return method.CreateDelegate<Func<object, CancellationToken, object>>();
    }
}
