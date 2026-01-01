using System.Buffers;
using System.Reflection;
using System.Reflection.Emit;
using StreamRpc.Collections;

namespace StreamRpc.TypeGeneration;

internal static class ImplementerCommon
{
    public static readonly MethodInfo Type_GetTypeFromHandle
            = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!;

    public static readonly MethodInfo Type_MakeGenericType
            = typeof(Type).GetMethod(nameof(Type.MakeGenericType))!;

    public static readonly MethodInfo MethodBase_GetMethodFromHandle
            = typeof(MethodBase).GetMethod(nameof(MethodBase.GetMethodFromHandle), [typeof(RuntimeMethodHandle)])!;

    public static readonly MethodInfo MethodBase_GetMethodFromHandleT
            = typeof(MethodBase).GetMethod(nameof(MethodBase.GetMethodFromHandle), [typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle)])!;

    public static readonly MethodInfo ThrowHelper_Fail = typeof(ThrowHelper).GetMethod(nameof(ThrowHelper.Fail))!;

    public const int SmallArrayMaxSize = 4;

    public static readonly Dictionary<int, Type> SmallArraysTypes = new()
    {
        { 0, typeof(SmallArray0<Type>) },
        { 1, typeof(SmallArray1<Type>) },
        { 2, typeof(SmallArray2<Type>) },
        { 3, typeof(SmallArray3<Type>) },
        { 4, typeof(SmallArray4<Type>) },
    };

    public static MethodInfo GetDeclaredMethod(this Type type, string name)
    {
        return type.GetTypeInfo().GetDeclaredMethod(name) ?? throw ThrowHelper.Unreachable;
    }

    public static IEnumerable<MethodInfo> GetDeclaredMethods(this Type type, string name)
    {
        return type.GetTypeInfo().GetDeclaredMethods(name);
    }

    public static PropertyInfo GetDeclaredProperty(this Type type, string name)
    {
        return type.GetTypeInfo().GetDeclaredProperty(name) ?? throw ThrowHelper.Unreachable;
    }

    public static ConstructorInfo GetDeclaredConstructor(this Type type)
    {
        return type.GetTypeInfo().DeclaredConstructors.Single() ?? throw ThrowHelper.Unreachable;
    }

    public static void CreateIgnoreAccessChecks(ModuleBuilder module)
    {
        const string attrName = "System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute";
        if (module.GetType(attrName) is { })
        {
            return;
        }

        var type = module.DefineType(attrName);
        var ctor = type.DefineConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                                          CallingConventions.HasThis,
                                          [typeof(string)]);

        ctor.DefineParameter(1, ParameterAttributes.None, "assemblyName");

        var il = ctor.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, typeof(Attribute).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null)!);
        il.Emit(OpCodes.Ret);

        var attrType = type.CreateType()!;
        var asm = (AssemblyBuilder)module.Assembly;
        asm.SetCustomAttribute(new CustomAttributeBuilder(attrType.GetConstructors().Single(), ["StreamRpc"]));
    }

    public static Type GetValueType(Type methodReturnType)
    {
        if (methodReturnType == typeof(void) || methodReturnType == typeof(ValueTask) || methodReturnType == typeof(Task))
        {
            return typeof(void);
        }

        if (!methodReturnType.IsGenericType)
        {
            return methodReturnType;
        }

        var genDef = methodReturnType.GetGenericTypeDefinition();
        if (genDef == typeof(ValueTask<>) || genDef == typeof(Task<>))
        {
            return methodReturnType.GetGenericArguments()[0];
        }

        return methodReturnType;
    }

    public static void DefineCtor(TypeBuilder typeBuilder, ConstructorInfo baseCtor)
    {
        var ctor = typeBuilder.DefineConstructor(
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                    CallingConventions.HasThis,
                    null);
        var il = ctor.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, baseCtor);
        il.Emit(OpCodes.Ret);
    }

    private static readonly Type[]?[] s_typesPool = new Type[4][];

    private static Type[] GetPooledArray(int size)
    {
        if (size == 0)
        {
            return Type.EmptyTypes;
        }

        if (size <= s_typesPool.Length)
        {
            return Interlocked.Exchange(ref s_typesPool[size - 1], null) ?? new Type[size];
        }
        return new Type[size];
    }

    private static void ReturnPooledArray(Type[] array)
    {
        if (array.Length == 0)
        {
            return;
        }

        if (array.Length <= s_typesPool.Length)
        {
            Array.Clear(array);
            Interlocked.Exchange(ref s_typesPool[array.Length - 1], array);
        }
    }

    public static Type Substitute(Type type, IReadOnlyDictionary<Type, Type> substitutions)
    {
        if (substitutions.Count == 0)
        {
            return type;
        }

        if (substitutions.TryGetValue(type, out var substitution))
        {
            return substitution;
        }

        if (type.IsGenericParameter)
        {
            // it would've substituted earlier
            return type;
        }

        // array, pointer or reference
        if (type.GetElementType() is { } elemType)
        {
            var sub = Substitute(elemType, substitutions);
            if (sub == elemType)
            {
                return sub;
            }

            if (type.IsArray)
            {
                if (type.IsSZArray)
                {
                    return sub.MakeArrayType();
                }
                else if (!type.IsVariableBoundArray)
                {
                    return sub.MakeArrayType(type.GetArrayRank());
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            if (type.IsPointer)
            {
                return sub.MakePointerType();
            }
            if (type.IsByRef)
            {
                return sub.MakeByRefType();
            }

            return sub;
        }

        if (type.IsGenericType)
        {
            var genArgs = type.GetGenericArguments();
            var result = GetPooledArray(genArgs.Length);
            try
            {
                for (int i = 0; i < genArgs.Length; i++)
                {
                    result[i] = Substitute(genArgs[i], substitutions);
                }

                if (genArgs.AsSpan().SequenceEqual(result))
                {
                    return type;
                }

                return type.GetGenericTypeDefinition().MakeGenericType(result);
            }
            finally
            {
                ReturnPooledArray(result);
            }
        }

        return type;
    }
}
