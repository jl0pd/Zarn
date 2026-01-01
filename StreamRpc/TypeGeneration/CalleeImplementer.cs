using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using StreamRpc.Collections;
using StreamRpc.Protocol;

namespace StreamRpc.TypeGeneration;

internal static class CalleeImplementer
{
    private static readonly ConcurrentDictionary<Type, Lazy<Type>> s_types = [];
    private static readonly ModuleBuilder s_module;
    private static readonly ConstructorInfo CalleeBase_ctor;
    private static readonly MethodInfo CalleeBase_ParseArgument;
    private static readonly MethodInfo CalleeBase_CompleteT;
    private static readonly MethodInfo CalleeBase_CompleteVoid;
    private static readonly MethodInfo CalleeBase_WaitTaskT;
    private static readonly MethodInfo CalleeBase_WaitVoidTask;
    private static readonly MethodInfo CalleeBase_DispatchCore;
    private static readonly MethodInfo CalleeBase_TryFindTrampoline;
    private static readonly MethodInfo CalleeBase_CreateTrampoline;
    private static readonly MethodInfo CalleeBase_Fail;
    private static readonly MethodInfo GenericMethodInvokeTrampoline_Invoke;
    private static readonly ConstructorInfo ValueTask_Ctor_Task;
    private static readonly MethodInfo MemoryExtensions_AsSpanT;
    private static readonly MethodInfo Type_op_Eqiality;
    private static readonly MethodInfo ReadOnlySpan_Type_get_Length;
    private static readonly MethodInfo ReadOnlySpan_Type_get_Item;

    static CalleeImplementer()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName()
        {
            Name = "Rpc assembly"
        }, AssemblyBuilderAccess.Run);

        s_module = assembly.DefineDynamicModule("<module>");

        ImplementerCommon.CreateIgnoreAccessChecks(s_module);

        CalleeBase_ctor = typeof(CalleeBase).GetTypeInfo().DeclaredConstructors.Single();
        CalleeBase_ParseArgument = typeof(CalleeBase).GetDeclaredMethod(nameof(CalleeBase.ParseArgument))!;
        CalleeBase_CompleteT = typeof(CalleeBase).GetDeclaredMethod(nameof(CalleeBase.Complete))!;
        CalleeBase_CompleteVoid = typeof(CalleeBase).GetDeclaredMethod(nameof(CalleeBase.CompleteVoid))!;
        CalleeBase_WaitTaskT = typeof(CalleeBase).GetDeclaredMethod(nameof(CalleeBase.WaitTask))!;
        CalleeBase_WaitVoidTask = typeof(CalleeBase).GetDeclaredMethod(nameof(CalleeBase.WaitVoidTask))!;
        CalleeBase_DispatchCore = typeof(CalleeBase).GetDeclaredMethod(nameof(CalleeBase.DispatchCore))!;
        CalleeBase_TryFindTrampoline = typeof(CalleeBase).GetDeclaredMethod(nameof(CalleeBase.TryFindTrampoline))!;
        CalleeBase_CreateTrampoline = typeof(CalleeBase).GetDeclaredMethod(nameof(CalleeBase.CreateTrampoline))!;
        CalleeBase_Fail = typeof(CalleeBase).GetDeclaredMethod(nameof(CalleeBase.Fail))!;
        GenericMethodInvokeTrampoline_Invoke = typeof(GenericMethodInvokeTrampoline).GetDeclaredMethod(nameof(GenericMethodInvokeTrampoline.Invoke));
        ValueTask_Ctor_Task = typeof(ValueTask).GetConstructor([typeof(Task)])!;
        MemoryExtensions_AsSpanT = typeof(MemoryExtensions).GetDeclaredMethods(nameof(MemoryExtensions.AsSpan)).First(x => x.GetParameters()[0].ParameterType.IsSZArray);
        Type_op_Eqiality = typeof(Type).GetMethod("op_Equality")!;
        ReadOnlySpan_Type_get_Length = typeof(ReadOnlySpan<Type>).GetMethod("get_Length")!;
        ReadOnlySpan_Type_get_Item = typeof(ReadOnlySpan<Type>).GetMethod("get_Item")!;
    }

    public static Type GetImplementation(Type interfaceType)
    {
        return s_types.GetOrAdd(interfaceType, t => new Lazy<Type>(() => ImplementType(t))).Value;
    }

    private static Type ImplementType(Type interfaceType)
    {
        var typeBuilder = s_module.DefineType(interfaceType.Name + "Impl", TypeAttributes.Sealed);
        typeBuilder.SetParent(typeof(CalleeBase));

        ImplementerCommon.DefineCtor(typeBuilder, CalleeBase_ctor);

        var implField = ImplementImplProp(typeBuilder, interfaceType);

        var stubs = ImplementInvokeStubs(typeBuilder, interfaceType, implField);

        ImplementDispatchCore(typeBuilder, stubs);

        return typeBuilder.CreateType();
    }

    private static void ImplementDispatchCore(TypeBuilder typeBuilder, MethodBuilder[] stubs)
    {
        /*
        internal protected override void DispatchCore(ref SequenceReader<byte> argumentsReader, int methodSlot)
        {
            switch (methodSlot)
            {
                case 0:
                    InvokeStub#0(ref argumentsReader);
                    return;
                case 1:
                    InvokeStub#1(ref argumentsReader);
                    return;
                default:
                    throw ThrowHelper.Fail("errMsg");
            }
        }
        */

        var method = typeBuilder.DefineMethod(
                        nameof(CalleeBase.DispatchCore),
                        MethodAttributes.FamORAssem | MethodAttributes.Virtual | MethodAttributes.Final,
                        typeof(void),
                        [typeof(SequenceReader<byte>).MakeByRefType(), typeof(int)]);

        var il = method.GetILGenerator();

        var branchTargets = new Label[stubs.Length];
        for (int i = 0; i < branchTargets.Length; i++)
        {
            branchTargets[i] = il.DefineLabel();
        }

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Switch, branchTargets);

        for (int i = 0; i < stubs.Length; i++)
        {
            il.MarkLabel(branchTargets[i]);
            il.Emit(OpCodes.Call, stubs[i]);
            il.Emit(OpCodes.Ret);
        }

        il.Emit(OpCodes.Ldstr, "Invalid method slot was passed for invoke");
        il.Emit(OpCodes.Call, ImplementerCommon.ThrowHelper_Fail);
        il.Emit(OpCodes.Throw);
    }

    private static MethodBuilder[] ImplementInvokeStubs(TypeBuilder typeBuilder, Type interfaceType, FieldBuilder implField)
    {
        var methods = interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        var result = new MethodBuilder[methods.Length];

        for (int i = 0; i < methods.Length; i++)
        {
            result[i] = ImplementInvokeStub(typeBuilder, methods[i], implField, i);
        }

        return result;
    }

    private static MethodBuilder ImplementInvokeStub(TypeBuilder typeBuilder, MethodInfo interfaceMethod, FieldBuilder implField, int i)
    {
        // private void InvokeStub#N(ref SequenceReader<byte> argumentsReader)
        var methodBuilder = typeBuilder.DefineMethod("InvokeStub#" + i,
                                                     MethodAttributes.Private,
                                                     typeof(void),
                                                     [typeof(SequenceReader<byte>).MakeByRefType()]);

        if (interfaceMethod.IsGenericMethod)
        {
            EmitTrampolineInvocation(typeBuilder, interfaceMethod, implField, methodBuilder);
        }
        else
        {
            EmitInvokeBody(interfaceMethod, implField, methodBuilder, []);
        }

        return methodBuilder;
    }

    private static void EmitInvokeBody(MethodInfo interfaceMethod, FieldBuilder implField, MethodBuilder methodBuilder, Type[] genericArgs)
    {
        /*
            var arg1 = base.ParseArgument<arg1Type>(ref argumentsReader);

            try
            {
                var res = _impl.InterfaceMethod(arg1);
                Complete(res);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        */

        Type returnType;
        Type[] parameterTypes;
        if (genericArgs.Length > 0)
        {
            var parameters = interfaceMethod.GetParameters();
            var subs = new Dictionary<Type, Type>(genericArgs.Length);
            var genArgs = interfaceMethod.GetGenericArguments();

            Debug.Assert(genArgs.Length == genericArgs.Length);
            for (int i = 0; i < genericArgs.Length; i++)
            {
                subs[genArgs[i]] = genericArgs[i];
            }

            returnType = ImplementerCommon.Substitute(interfaceMethod.ReturnType, subs);
            parameterTypes = new Type[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                parameterTypes[i] = ImplementerCommon.Substitute(parameters[i].ParameterType, subs);
            }
        }
        else
        {
            returnType = interfaceMethod.ReturnType;
            parameterTypes = interfaceMethod.GetParameters().Select(x => x.ParameterType).ToArray();
        }

        var resultType = ImplementerCommon.GetValueType(returnType);

        var il = methodBuilder.GetILGenerator();

        var retLabel = il.DefineLabel();
        var exLocal = il.DeclareLocal(typeof(Exception));

        // evaluation stack must be empty when entering exception block, so free it and restore when we're inside
        var locals = new List<LocalBuilder>(parameterTypes.Length);

        foreach (var param in parameterTypes)
        {
            emitCalleeRef();
            il.Emit(genericArgs.Length > 0 ? OpCodes.Ldarg_2 : OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, CalleeBase_ParseArgument.MakeGenericMethod(param));

            var local = il.DeclareLocal(param);
            il.Emit(OpCodes.Stloc, local);
            locals.Add(local);
        }

        // try
        il.BeginExceptionBlock();
        {
            emitCalleeRef();

            emitCalleeRef();
            il.Emit(OpCodes.Ldfld, implField);

            foreach (var local in locals)
            {
                il.Emit(OpCodes.Ldloc, local);
            }

            // _impl.InterfaceMethod(arg0, arg1, argN)
            if (genericArgs.Length > 0)
            {
                il.Emit(OpCodes.Callvirt, interfaceMethod.MakeGenericMethod(genericArgs));
            }
            else
            {
                il.Emit(OpCodes.Callvirt, interfaceMethod);
            }

            if (returnType == typeof(void))
            {
                il.Emit(OpCodes.Call, CalleeBase_CompleteVoid);
            }
            else if (returnType == typeof(ValueTask))
            {
                il.Emit(OpCodes.Call, CalleeBase_WaitVoidTask);
            }
            else if (returnType == typeof(Task))
            {
                // WaitVoidTask(new ValueTask(res));
                il.Emit(OpCodes.Newobj, ValueTask_Ctor_Task);
                il.Emit(OpCodes.Call, CalleeBase_WaitVoidTask);
            }
            else if (returnType.IsGenericType)
            {
                var typeDef = returnType.GetGenericTypeDefinition();
                if (typeDef == typeof(ValueTask<>))
                {
                    il.Emit(OpCodes.Call, CalleeBase_WaitTaskT.MakeGenericMethod(resultType));
                }
                else if (typeDef == typeof(Task<>))
                {
                    // WaitTask<T>(new ValueTask<T>(res));
                    var ctor = typeof(ValueTask<>)
                                .MakeGenericType(resultType)
                                .GetConstructor([typeof(Task<>).MakeGenericType(resultType)])!;
                    il.Emit(OpCodes.Newobj, ctor);
                    il.Emit(OpCodes.Call, CalleeBase_WaitTaskT.MakeGenericMethod(resultType));
                }
                else
                {
                    il.Emit(OpCodes.Call, CalleeBase_CompleteT.MakeGenericMethod(resultType));
                }
            }
            else
            {
                il.Emit(OpCodes.Call, CalleeBase_CompleteT.MakeGenericMethod(resultType));
            }
            il.Emit(OpCodes.Leave, retLabel);
        }

        // catch(Exception e) { Fail(e); }
        il.BeginCatchBlock(typeof(Exception));
        {
            il.Emit(OpCodes.Stloc, exLocal);
            emitCalleeRef();
            il.Emit(OpCodes.Ldloc, exLocal);
            il.Emit(OpCodes.Call, CalleeBase_Fail);

            il.Emit(OpCodes.Leave, retLabel);
        }
        il.EndExceptionBlock();

        il.MarkLabel(retLabel);
        il.Emit(OpCodes.Ret);

        void emitCalleeRef() => il.Emit(genericArgs.Length > 0 ? OpCodes.Ldarg_1 : OpCodes.Ldarg_0);
    }

    private static void EmitTrampolineInvocation(TypeBuilder typeBuilder, MethodInfo interfaceMethod, FieldBuilder implField, MethodBuilder methodBuilder)
    {
        /*
            private SingleLinkedListNode<GenericMethodInvokeTrampoline>? InvokeStub#N_Trampolines;
            private void InvokeStub#N(ref SequenceReader<byte> argumentsReader)
            {
                var genArgs = base.ParseArgument<SmallArrayN<Type>>(ref argumentsReader).AsSpan();
                var trampoline = base.TryFindTrampoline(ref InvokeStub#N_Trampolines, genArgs)
                              ?? base.CreateTrampoline(
                                        ref InvokeStub#N_Trampolines,
                                        typeof(GenericMethodInvokeTrampoline#N<>).MakeGenericType(genArgs.ToArray()));

                trampoline.Invoke(this, ref argumentsReader);
            }

            private sealed class InvokeStub#N_Trampoline<T0, T1, ...> : GenericMethodInvokeTrampoline
            {
                private readonly Type _type0 = typeof(T0);
                private readonly Type _type1 = typeof(T1);
                private readonly Type _typeN = typeof(TN);

                public override void Invoke(CalleeBase callee, ref SequenceReader<byte> argumentsReader)
                {
                    var arg1 = callee.ParseArgument<arg1Type>(ref argumentsReader);

                    try
                    {
                        var res = ((CalleeImpl)callee)._impl.InterfaceMethod<T0, T1, ...>(arg1);
                        callee.Complete(res);
                    }
                    catch (Exception e)
                    {
                        callee.Fail(e);
                    }
                }

                public override bool Matches(ReadOnlySpan<Type> genericArgs)
                {
                    return genericArgs.Length == N
                        && genericArgs[0] == _type0
                        && genericArgs[N] == _typeN;
                }
            }
        */

        var trampolineBuilder = typeBuilder.DefineNestedType(methodBuilder.Name + "_Trampoline",
                                                          TypeAttributes.NestedPrivate,
                                                          typeof(GenericMethodInvokeTrampoline));

        ImplementTrampolineType(trampolineBuilder, interfaceMethod, implField);

        trampolineBuilder.CreateType();

        var trampolinesField = typeBuilder.DefineField(methodBuilder.Name + "_Trampolines",
                                                       typeof(SingleLinkedListNode<GenericMethodInvokeTrampoline>),
                                                       FieldAttributes.Private);

        var genArgsCount = interfaceMethod.GetGenericArguments().Length;

        var il = methodBuilder.GetILGenerator();
        var invokeLabel = il.DefineLabel();

        // var genArgs = base.ParseArguments<SmallArrayN<Type>>(ref argumentsReader).AsSpan();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        if (genArgsCount <= ImplementerCommon.SmallArrayMaxSize)
        {
            var saType = ImplementerCommon.SmallArraysTypes[genArgsCount];
            var saLocal = il.DeclareLocal(saType);
            il.Emit(OpCodes.Callvirt, CalleeBase_ParseArgument.MakeGenericMethod(saType));
            il.Emit(OpCodes.Stloc, saLocal);
            il.Emit(OpCodes.Ldloca, saLocal);
            il.Emit(OpCodes.Call, saType.GetMethod("AsSpan")!);
        }
        else
        {
            il.Emit(OpCodes.Call, CalleeBase_ParseArgument.MakeGenericMethod(typeof(Type[])));
            il.Emit(OpCodes.Call, MemoryExtensions_AsSpanT.MakeGenericMethod(typeof(Type)));
        }
        var genArgsLocal = il.DeclareLocal(typeof(ReadOnlySpan<Type>));
        il.Emit(OpCodes.Stloc, genArgsLocal);

        // base.TryFindTrampoline(ref InvokeStub#N_Trampolines, genArgs);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldflda, trampolinesField);
        il.Emit(OpCodes.Ldloc, genArgsLocal);
        il.Emit(OpCodes.Callvirt, CalleeBase_TryFindTrampoline);

        // ?? base.CreateTrampoline(ref InvokeStub#N_Trampolines,
        //                          typeof(GenericMethodInvokeTrampoline#N<>).MakeGenericType(genArgs.ToArray()))
        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Brtrue, invokeLabel);
        il.Emit(OpCodes.Pop);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldflda, trampolinesField);
        // typeof(GenericMethodInvokeTrampoline#N<>).MakeGenericType(genArgs.ToArray())
        il.Emit(OpCodes.Ldtoken, trampolineBuilder);
        il.Emit(OpCodes.Call, ImplementerCommon.Type_GetTypeFromHandle);
        il.Emit(OpCodes.Ldloca, genArgsLocal);
        il.Emit(OpCodes.Call, typeof(Span<Type>).GetMethod(nameof(Span<Type>.ToArray))!);
        il.Emit(OpCodes.Callvirt, ImplementerCommon.Type_MakeGenericType);
        il.Emit(OpCodes.Callvirt, CalleeBase_CreateTrampoline);

        // trampoline.Invoke(this, ref argumentsReader);
        il.MarkLabel(invokeLabel);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Callvirt, GenericMethodInvokeTrampoline_Invoke);

        il.Emit(OpCodes.Ret);
    }

    private static void ImplementTrampolineType(TypeBuilder trampolineType, MethodInfo interfaceMethod, FieldBuilder implField)
    {
        var genParams = trampolineType.DefineGenericParameters(interfaceMethod.GetGenericArguments().Select(x => x.Name).ToArray());
        var fields = ImplementTrampolineCtor(genParams, trampolineType, interfaceMethod);
        ImplementTrampolineMatches(fields, trampolineType);
        ImplementTrampolineInvoke(genParams, trampolineType, interfaceMethod, implField);
    }

    private static void ImplementTrampolineInvoke(GenericTypeParameterBuilder[] genParams, TypeBuilder trampolineType, MethodInfo interfaceMethod, FieldBuilder implField)
    {
        /*
            public override void Invoke(CalleeBase callee, ref SequenceReader<byte> argumentsReader)
            {
                var arg1 = callee.ParseArgument<arg1Type>(ref argumentsReader);

                try
                {
                    var res = ((CalleeImpl)callee)._impl.InterfaceMethod<T0, T1, ...>(arg1);
                    callee.Complete(res);
                }
                catch (Exception e)
                {
                    callee.Fail(e);
                }
            }
        */

        var invokeMethod = trampolineType.DefineMethod(nameof(GenericMethodInvokeTrampoline.InvokeCore),
                                                       MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final,
                                                       CallingConventions.HasThis,
                                                       typeof(void),
                                                       [typeof(CalleeBase), typeof(SequenceReader<byte>).MakeByRefType()]);

        EmitInvokeBody(interfaceMethod, implField, invokeMethod, genParams);
    }

    private static FieldBuilder[] ImplementTrampolineCtor(GenericTypeParameterBuilder[] genParams, TypeBuilder trampolineType, MethodInfo interfaceMethod)
    {
        var ctor = trampolineType.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, null);

        var il = ctor.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, typeof(GenericMethodInvokeTrampoline).GetDeclaredConstructor());

        var result = new FieldBuilder[genParams.Length];
        for (int i = 0; i < genParams.Length; i++)
        {
            var field = trampolineType.DefineField("_type" + i, typeof(Type), FieldAttributes.InitOnly | FieldAttributes.Private);

            // this._typeN = typeof(T_N);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldtoken, genParams[i]);
            il.Emit(OpCodes.Call, ImplementerCommon.Type_GetTypeFromHandle);
            il.Emit(OpCodes.Stfld, field);

            result[i] = field;
        }

        il.Emit(OpCodes.Ret);

        return result;
    }

    private static void ImplementTrampolineMatches(FieldBuilder[] fields, TypeBuilder trampolineType)
    {
        /*
            public override bool Matches(ReadOnlySpan<Type> genericArgs)
            {
                return genericArgs.Length == N
                    && genericArgs[0] == _type0
                    && genericArgs[N] == _typeN;
            }
        */
        var methodBuilder = trampolineType.DefineMethod(nameof(GenericMethodInvokeTrampoline.Matches),
                                                        MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final,
                                                        CallingConventions.HasThis,
                                                        typeof(bool),
                                                        [typeof(ReadOnlySpan<Type>)]);

        var il = methodBuilder.GetILGenerator();
        var retLabel = il.DefineLabel();

        il.Emit(OpCodes.Ldarga, 1);
        il.Emit(OpCodes.Call, ReadOnlySpan_Type_get_Length);
        il.Emit(OpCodes.Ldc_I4, fields.Length);
        il.Emit(OpCodes.Ceq);

        for (int i = 0; i < fields.Length; i++)
        {
            // && genericArgs[N] == _typeN;
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Brfalse, retLabel);
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ldarga, 1);
            il.Emit(OpCodes.Ldc_I4, i);
            il.Emit(OpCodes.Call, ReadOnlySpan_Type_get_Item);
            il.Emit(OpCodes.Ldind_Ref);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, fields[i]);
            il.Emit(OpCodes.Call, Type_op_Eqiality);
        }

        il.MarkLabel(retLabel);
        il.Emit(OpCodes.Ret);
    }

    private static FieldBuilder ImplementImplProp(TypeBuilder typeBuilder, Type interfaceType)
    {
        /*
        internal protected override object Impl
        {
            get => _impl;
            set => _impl = (InterfaceType)value;
        }
        private InterfaceType _impl;
        */

        var field = typeBuilder.DefineField("_impl", interfaceType, FieldAttributes.Private);

        var getter = typeBuilder.DefineMethod(
                        "get_" + nameof(CalleeBase.Impl),
                        MethodAttributes.FamORAssem | MethodAttributes.Final | MethodAttributes.Virtual,
                        CallingConventions.HasThis,
                        typeof(object),
                        null);

        {
            var il = getter.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ret);
        }

        var setter = typeBuilder.DefineMethod(
                        "set_" + nameof(CalleeBase.Impl),
                        MethodAttributes.FamORAssem | MethodAttributes.Final | MethodAttributes.Virtual,
                        CallingConventions.HasThis,
                        typeof(void),
                        [typeof(object)]);
        {
            var il = setter.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Castclass, interfaceType);
            il.Emit(OpCodes.Stfld, field);
            il.Emit(OpCodes.Ret);
        }

        var prop = typeBuilder.DefineProperty(nameof(CalleeBase.Impl), PropertyAttributes.None, typeof(object), null);

        prop.SetGetMethod(getter);
        prop.SetSetMethod(setter);

        return field;
    }
}
