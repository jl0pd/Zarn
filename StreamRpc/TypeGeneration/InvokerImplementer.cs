using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using StreamRpc.Collections;
using StreamRpc.Protocol;

namespace StreamRpc.TypeGeneration;

internal static class InvokerImplementer
{
    private static readonly ConcurrentDictionary<Type, Lazy<Type>> s_types = [];
    private static readonly ConcurrentDictionary<Type, Lazy<Type>> s_finalizableTypes = [];
    private static readonly ModuleBuilder s_module;
    private static readonly ConstructorInfo InvokerBase_ctor;
    private static readonly MethodInfo InvokerBase_CreateOperationT;
    private static readonly MethodInfo InvokerBase_CreateVoidOperation;
    private static readonly MethodInfo InvokerBase_SynchronousWaitResultT;
    private static readonly MethodInfo InvokerBase_SynchronousWaitVoidResult;
    private static readonly MethodInfo InvokerBase_GetMethodSlot;
    private static readonly MethodInfo InvokerOperation_SerializeArgT;
    private static readonly MethodInfo InvokerOperation_SetMethodSlot;
    private static readonly MethodInfo InvokerOperation_Prepare;
    private static readonly MethodInfo InvokerOperation_SetCancellationToken;
    private static readonly MethodInfo InvokerOperationT_Start;
    private static readonly MethodInfo VoidInvokerOperation_Start;
    private static readonly MethodInfo CancellationToken_ThrowIfCancellationRequested;
    private static readonly MethodInfo ValueTask_AsTask;
    private static readonly MethodInfo SmallArray_Create1_Type;
    private static readonly MethodInfo SmallArray_Create2_Type;
    private static readonly MethodInfo SmallArray_Create3_Type;
    private static readonly MethodInfo SmallArray_Create4_Type;

    static InvokerImplementer()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName()
        {
            Name = "Rpc assembly"
        }, AssemblyBuilderAccess.Run);

        s_module = assembly.DefineDynamicModule("<module>");

        ImplementerCommon.CreateIgnoreAccessChecks(s_module);

        InvokerBase_ctor = typeof(InvokerBase).GetDeclaredConstructor();
        InvokerBase_CreateOperationT = typeof(InvokerBase).GetDeclaredMethod(nameof(InvokerBase.CreateOperation))!;
        InvokerBase_CreateVoidOperation = typeof(InvokerBase).GetDeclaredMethod(nameof(InvokerBase.CreateVoidOperation))!;
        InvokerBase_SynchronousWaitResultT = typeof(InvokerBase).GetDeclaredMethod(nameof(InvokerBase.SynchronousWaitResult))!;
        InvokerBase_SynchronousWaitVoidResult = typeof(InvokerBase).GetDeclaredMethod(nameof(InvokerBase.SynchronousWaitVoidResult))!;
        InvokerBase_GetMethodSlot = typeof(InvokerBase).GetDeclaredMethod(nameof(InvokerBase.GetMethodSlot))!;

        InvokerOperation_SerializeArgT = typeof(InvokerOperation).GetDeclaredMethod(nameof(InvokerOperation.SerializeArg))!;
        InvokerOperation_SetMethodSlot = typeof(InvokerOperation).GetDeclaredProperty(nameof(InvokerOperation.MethodSlot))!.GetSetMethod()!;
        InvokerOperation_Prepare = typeof(InvokerOperation).GetDeclaredMethod(nameof(InvokerOperation.Prepare))!;
        InvokerOperation_SetCancellationToken = typeof(InvokerOperation).GetDeclaredProperty(nameof(InvokerOperation.CancellationToken))!.GetSetMethod()!;
        InvokerOperationT_Start = typeof(InvokerOperation<>).GetDeclaredMethod(nameof(InvokerOperation<int>.Start))!;
        VoidInvokerOperation_Start = typeof(VoidInvokerOperation).GetDeclaredMethod(nameof(VoidInvokerOperation.Start))!;

        CancellationToken_ThrowIfCancellationRequested = typeof(CancellationToken).GetMethod(nameof(CancellationToken.ThrowIfCancellationRequested))!;
        ValueTask_AsTask = typeof(ValueTask).GetMethod(nameof(ValueTask.AsTask))!;

        SmallArray_Create1_Type = typeof(SmallArray).GetDeclaredMethods(nameof(SmallArray.Create)).First(x => x.GetParameters().Length == 1).MakeGenericMethod(typeof(Type));
        SmallArray_Create2_Type = typeof(SmallArray).GetDeclaredMethods(nameof(SmallArray.Create)).First(x => x.GetParameters().Length == 2).MakeGenericMethod(typeof(Type));
        SmallArray_Create3_Type = typeof(SmallArray).GetDeclaredMethods(nameof(SmallArray.Create)).First(x => x.GetParameters().Length == 3).MakeGenericMethod(typeof(Type));
        SmallArray_Create4_Type = typeof(SmallArray).GetDeclaredMethods(nameof(SmallArray.Create)).First(x => x.GetParameters().Length == 4).MakeGenericMethod(typeof(Type));
    }

    public static Type GetImplementation(Type interfaceType)
    {
        return s_types.GetOrAdd(interfaceType, t => new Lazy<Type>(() => ImplementType(t, false))).Value;
    }

    public static Type GetFinalizableImplementation(Type interfaceType)
    {
        return s_finalizableTypes.GetOrAdd(interfaceType, t => new Lazy<Type>(() => ImplementType(t, true))).Value;
    }

    private static Type ImplementType(Type interfaceType, bool finalizable)
    {
        var typeBuilder = s_module.DefineType(interfaceType.Name + (finalizable ? "Proxy" : "") + "Impl",
                                              TypeAttributes.Sealed);
        typeBuilder.SetParent(finalizable ? typeof(FinalizableInvokerBase) : typeof(InvokerBase));
        typeBuilder.AddInterfaceImplementation(interfaceType);

        ImplementerCommon.DefineCtor(typeBuilder, InvokerBase_ctor);

        var methods = interfaceType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
        for (int i = 0; i < methods.Length; i++)
        {
            ImplementMethod(typeBuilder, methods[i], i);
        }

        return typeBuilder.CreateType();
    }

    private static void ImplementMethod(TypeBuilder typeBuilder, MethodInfo interfaceMethod, int methodIndex)
    {
        /*
        // throw at start of method to avoid allocation of InvokerOperation
        if (methodHasCancellationTokenArg)
        {
            argCancellationToken.ThrowIfCancellationRequested();
        }

        var op = base.CreateOperation<retType>();

        if (methodHasCancellationTokenArg)
        {
            op.CancellationToken = argCancellationToken;
        }

        op.MethodSlot = base.GetMethodSlot(_methodInfo ??= methodof(interfaceMethod));
        op.Prepare();
        if (methodIsGeneric)
        {
            if (methodGenArgs.Length <= 4)
            {
                op.SerializeArg<SmallArrayN<Type>>(SmallArray.Create(methodGenArg0, methodGenArg1, ...));
            }
            else
            {
                op.SerializeArg<Type[]>(new Type[] { methodGenArg0, methodGenArg1, ... });
            }
        }

        op.SerializeArg<arg1Type>(arg1);
        op.SerializeArg<arg2Type>(arg2);
        op.SerializeArg<argNType>(argN);

        ValueTask<retType> task = op.Start();
        if (methodIsAsync)
        {
            return task; // or task.AsTask();
        }
        else 
        {
            return base.SynchronousWaitResult(task);
        }
        */
        var method = typeBuilder.DefineMethod(
                interfaceMethod.Name,
                MethodAttributes.Public | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.Final);

        var genArgs = Array.Empty<GenericTypeParameterBuilder>();
        IReadOnlyDictionary<Type, Type> substitutions = ImmutableDictionary<Type, Type>.Empty;
        if (interfaceMethod.IsGenericMethod)
        {
            var interfaceGenArgs = interfaceMethod.GetGenericArguments();
            genArgs = method.DefineGenericParameters(interfaceGenArgs.Select(x => x.Name).ToArray());
            var subs = new Dictionary<Type, Type>(genArgs.Length);
            for (int i = 0; i < interfaceGenArgs.Length; i++)
            {
                subs[interfaceGenArgs[i]] = genArgs[i];
            }

            substitutions = subs;
        }

        method.SetReturnType(ImplementerCommon.Substitute(interfaceMethod.ReturnType, substitutions));
        var parameters = interfaceMethod.GetParameters();
        if (parameters.Length > 0 && Array.Find(parameters, x => x.ParameterType.IsGenericParameter) is { })
        {
            var substitutedParameters = new Type[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                substitutedParameters[i] = ImplementerCommon.Substitute(parameters[i].ParameterType, substitutions);
            }
            method.SetParameters(substitutedParameters);
        }
        else
        {
            method.SetParameters(parameters.Select(x => x.ParameterType).ToArray());
        }

        var methodInfoField = typeBuilder.DefineField("_methodInfo#" + methodIndex, typeof(MethodInfo), FieldAttributes.Private);

        var returnValueType = ImplementerCommon.GetValueType(interfaceMethod.ReturnType);

        var cancellationTokenParameter = parameters.FirstOrDefault(p => p.ParameterType == typeof(CancellationToken));

        var il = method.GetILGenerator();

        var opLocal = il.DeclareLocal(returnValueType == typeof(void)
                                        ? typeof(VoidInvokerOperation)
                                        : typeof(InvokerOperation<>).MakeGenericType([returnValueType]));

        if (cancellationTokenParameter is { })
        {
            il.Emit(OpCodes.Ldarga, cancellationTokenParameter.Position + 1);
            il.Emit(OpCodes.Call, CancellationToken_ThrowIfCancellationRequested);
        }

        // var op = base.CreateOperation<T>();
        il.Emit(OpCodes.Ldarg_0);
        if (returnValueType == typeof(void))
        {
            il.Emit(OpCodes.Call, InvokerBase_CreateVoidOperation);
        }
        else
        {
            il.Emit(OpCodes.Call, InvokerBase_CreateOperationT.MakeGenericMethod([returnValueType]));
        }
        il.Emit(OpCodes.Stloc, opLocal);

        if (cancellationTokenParameter is { })
        {
            // op.CancellationToken = cancellationTokenArg;
            il.Emit(OpCodes.Ldloc, opLocal);
            il.Emit(OpCodes.Ldarg, cancellationTokenParameter.Position + 1);
            il.Emit(OpCodes.Call, InvokerOperation_SetCancellationToken);
        }

        // op.MethodSlot = base.GetMethodSlot(_methodInfo ??= methodof(interfaceMethod));
        il.Emit(OpCodes.Ldloc, opLocal);
        il.Emit(OpCodes.Ldarg_0);
        {
            var ifNotNull = il.DefineLabel();

            // _methodInfo ??= methodof(interfaceMethod)
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, methodInfoField);
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Brtrue, ifNotNull);
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldtoken, interfaceMethod);
            il.Emit(OpCodes.Call, ImplementerCommon.MethodBase_GetMethodFromHandle);
            il.Emit(OpCodes.Stfld, methodInfoField);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, methodInfoField);
            il.MarkLabel(ifNotNull);
        }
        il.Emit(OpCodes.Call, InvokerBase_GetMethodSlot);
        il.Emit(OpCodes.Call, InvokerOperation_SetMethodSlot);

        // op.Prepare();
        il.Emit(OpCodes.Ldloc, opLocal);
        il.Emit(OpCodes.Call, InvokerOperation_Prepare);

        if (interfaceMethod.IsGenericMethod)
        {
            Debug.Assert(genArgs.Length >= 0);

            il.Emit(OpCodes.Ldloc, opLocal);

            if (genArgs.Length <= ImplementerCommon.SmallArrayMaxSize)
            {
                foreach (var arg in genArgs)
                {
                    il.Emit(OpCodes.Ldtoken, arg);
                    il.Emit(OpCodes.Call, ImplementerCommon.Type_GetTypeFromHandle);
                }
            }

            switch (genArgs.Length)
            {
                case 1:
                    // op.SerializeArg(SmallArray.Create(methodGenArg0));
                    il.Emit(OpCodes.Call, SmallArray_Create1_Type);
                    il.Emit(OpCodes.Callvirt, InvokerOperation_SerializeArgT.MakeGenericMethod(typeof(SmallArray1<Type>)));
                    break;
                case 2:
                    // op.SerializeArg(SmallArray.Create(methodGenArg0, methodGenArg1));
                    il.Emit(OpCodes.Call, SmallArray_Create2_Type);
                    il.Emit(OpCodes.Callvirt, InvokerOperation_SerializeArgT.MakeGenericMethod(typeof(SmallArray2<Type>)));
                    break;
                case 3:
                    // op.SerializeArg(SmallArray.Create(methodGenArg0, methodGenArg1, methodGenArg2));
                    il.Emit(OpCodes.Call, SmallArray_Create3_Type);
                    il.Emit(OpCodes.Callvirt, InvokerOperation_SerializeArgT.MakeGenericMethod(typeof(SmallArray3<Type>)));
                    break;
                case 4:
                    // op.SerializeArg(SmallArray.Create(methodGenArg0, methodGenArg1, methodGenArg2, methodGenArg3));
                    il.Emit(OpCodes.Call, SmallArray_Create4_Type);
                    il.Emit(OpCodes.Callvirt, InvokerOperation_SerializeArgT.MakeGenericMethod(typeof(SmallArray4<Type>)));
                    break;
                default:
                    // op.SerializeArg<Type[]>(new Type[] { methodGenArg0, methodGenArg1, ... });

                    // new Type[genArgs.Length]
                    il.Emit(OpCodes.Ldc_I4, genArgs.Length);
                    il.Emit(OpCodes.Newarr, typeof(Type));

                    for (int i = 0; i < genArgs.Length; i++)
                    {
                        // typeAr[i] = typeof(genArgs[i])
                        il.Emit(OpCodes.Dup);
                        il.Emit(OpCodes.Ldc_I4, i);
                        il.Emit(OpCodes.Ldtoken, genArgs[i]);
                        il.Emit(OpCodes.Call, ImplementerCommon.Type_GetTypeFromHandle);
                        il.Emit(OpCodes.Stelem_Ref);
                    }
                    il.Emit(OpCodes.Callvirt, InvokerOperation_SerializeArgT.MakeGenericMethod(typeof(Type[])));
                    break;
            }
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            // base.SerializationContext.Serialize(parameterN, bufferWriter);
            il.Emit(OpCodes.Ldloc, opLocal);
            il.Emit(OpCodes.Ldarg, i + 1);
            il.Emit(OpCodes.Call, InvokerOperation_SerializeArgT.MakeGenericMethod(parameters[i].ParameterType));
        }

        // var task = base.CompleteCall<retType>(bufferWriter, MessageOptions.None, operation, cancellationToken);
        il.Emit(OpCodes.Ldloc, opLocal);
        if (returnValueType == typeof(void))
        {
            il.Emit(OpCodes.Call, VoidInvokerOperation_Start);
        }
        else
        {
            il.Emit(OpCodes.Call, typeof(InvokerOperation<>).MakeGenericType(returnValueType).GetDeclaredMethod(nameof(InvokerOperation<int>.Start))!);
        }

        if (interfaceMethod.ReturnType == typeof(void))
        {
            // SynchronousWaitVoidResult(task);
            // return;

            il.Emit(OpCodes.Call, InvokerBase_SynchronousWaitVoidResult);
        }
        else if (interfaceMethod.ReturnType == typeof(ValueTask))
        {
            // return value as-is
        }
        else if (interfaceMethod.ReturnType == typeof(Task))
        {
            // return task.AsTask();
            var local = il.DeclareLocal(typeof(ValueTask));
            il.Emit(OpCodes.Stloc, local);
            il.Emit(OpCodes.Ldloca, local);
            il.Emit(OpCodes.Call, ValueTask_AsTask);
        }
        else if (interfaceMethod.ReturnType.IsGenericType)
        {
            var typeDef = interfaceMethod.ReturnType.GetGenericTypeDefinition();
            if (typeDef == typeof(ValueTask<>))
            {
                // return value as-is
            }
            else if (typeDef == typeof(Task<>))
            {
                // return task.AsTask();
                var local = il.DeclareLocal(GetCallResultLocalType(method.ReturnType));
                il.Emit(OpCodes.Stloc, local);
                il.Emit(OpCodes.Ldloca, local);
                il.Emit(OpCodes.Call, typeof(ValueTask<>)
                                        .MakeGenericType(returnValueType)
                                        .GetMethod(nameof(ValueTask.AsTask))!);
            }
            else
            {
                // not a task type
                // return SynchronousWaitResult<retType>(task);
                il.Emit(OpCodes.Call, InvokerBase_SynchronousWaitResultT.MakeGenericMethod(interfaceMethod.ReturnType));
            }
        }
        else
        {
            // return SynchronousWaitResult<retType>(task);
            il.Emit(OpCodes.Call, InvokerBase_SynchronousWaitResultT.MakeGenericMethod(interfaceMethod.ReturnType));
        }

        il.Emit(OpCodes.Ret);
    }

    private static Type GetCallResultLocalType(Type returnType)
    {
        var retType = ImplementerCommon.GetValueType(returnType);
        if (retType == typeof(void))
        {
            return typeof(ValueTask);
        }
        else
        {
            return typeof(ValueTask<>).MakeGenericType(retType);
        }
    }
}
