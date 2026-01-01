using System.Buffers;
using System.Reflection;
using System.Reflection.Emit;

namespace StreamRpc.TypeGeneration;

internal static class ImplementerCommon
{
    public static readonly MethodInfo Type_GetTypeFromHandle
            = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!;

    public static readonly MethodInfo MethodBase_GetMethodFromHandle
            = typeof(MethodBase).GetMethod(nameof(MethodBase.GetMethodFromHandle), [typeof(RuntimeMethodHandle)])!;

    public static readonly MethodInfo MethodBase_GetMethodFromHandleT
            = typeof(MethodBase).GetMethod(nameof(MethodBase.GetMethodFromHandle), [typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle)])!;

    public static readonly MethodInfo ThrowHelper_Fail = typeof(ThrowHelper).GetMethod(nameof(ThrowHelper.Fail))!;

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
            var result = ArrayPool<Type>.Shared.Rent(genArgs.Length);
            try
            {
                for (int i = 0; i < genArgs.Length; i++)
                {
                    result[i] = Substitute(genArgs[i], substitutions);
                }

                if (genArgs.AsSpan().SequenceEqual(result.AsSpan(0, genArgs.Length)))
                {
                    return type;
                }

                return type.GetGenericTypeDefinition().MakeGenericType(result);
            }
            finally
            {
                Array.Clear(result, 0, genArgs.Length);
                ArrayPool<Type>.Shared.Return(result);
            }
        }

        return type;
    }
}
