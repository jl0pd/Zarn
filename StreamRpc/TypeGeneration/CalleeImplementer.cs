using System.Buffers;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
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
    private static readonly MethodInfo CalleeBase_Fail;
    private static readonly ConstructorInfo ValueTask_Ctor_Task;

    static CalleeImplementer()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName()
        {
            Name = "Rpc assembly"
        }, AssemblyBuilderAccess.Run);

        s_module = assembly.DefineDynamicModule("<module>");

        ImplementerCommon.CreateIgnoreAccessChecks(s_module);

        var typeInfo = typeof(CalleeBase).GetTypeInfo();
        CalleeBase_ctor = typeInfo.DeclaredConstructors.Single();
        CalleeBase_ParseArgument = typeInfo.GetDeclaredMethod(nameof(CalleeBase.ParseArgument))!;
        CalleeBase_CompleteT = typeInfo.GetDeclaredMethod(nameof(CalleeBase.Complete))!;
        CalleeBase_CompleteVoid = typeInfo.GetDeclaredMethod(nameof(CalleeBase.CompleteVoid))!;
        CalleeBase_WaitTaskT = typeInfo.GetDeclaredMethod(nameof(CalleeBase.WaitTask))!;
        CalleeBase_WaitVoidTask = typeInfo.GetDeclaredMethod(nameof(CalleeBase.WaitVoidTask))!;
        CalleeBase_DispatchCore = typeInfo.GetDeclaredMethod(nameof(CalleeBase.DispatchCore))!;
        CalleeBase_Fail = typeInfo.GetDeclaredMethod(nameof(CalleeBase.Fail))!;
        ValueTask_Ctor_Task = typeof(ValueTask).GetConstructor([typeof(Task)])!;
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
        ImplementerCommon.DefineImplementedInterfaceProp(typeBuilder, interfaceType);

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
                    _0(ref argumentsReader);
                    return;
                case 1:
                    _1(ref argumentsReader);
                    return;
                default:
                    ThrowHelper.Fail("errMsg");
                    return;
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
        il.Emit(OpCodes.Ret);
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
        /*
        private void InvokeStub#N(ref SequenceReader<byte> argumentsReader)
        {
            var arg1 = base.ParseArgument<arg1Type>(ref argumentsReader);
            var res = _impl.InterfaceMethod(arg1);

            try
            {
                Complete(res);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }
        */

        var methodBuilder = typeBuilder.DefineMethod("InvokeStub#" + i,
                                                     MethodAttributes.Private,
                                                     typeof(void),
                                                     [typeof(SequenceReader<byte>).MakeByRefType()]);

        var parameters = interfaceMethod.GetParameters();
        var resultType = ImplementerCommon.GetValueType(interfaceMethod.ReturnType);

        var il = methodBuilder.GetILGenerator();

        var retLabel = il.DefineLabel();
        var exLocal = il.DeclareLocal(typeof(Exception));

        // evaluation stack must be empty when entering exception block, so free it and restore when we're inside
        var locals = new List<LocalBuilder>(parameters.Length);

        foreach (var param in interfaceMethod.GetParameters())
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, CalleeBase_ParseArgument.MakeGenericMethod(param.ParameterType));

            var local = il.DeclareLocal(param.ParameterType);
            il.Emit(OpCodes.Stloc, local);
            locals.Add(local);
        }

        // try
        il.BeginExceptionBlock();
        {
            il.Emit(OpCodes.Ldarg_0);

            // this._impl
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, implField);

            foreach (var local in locals)
            {
                il.Emit(OpCodes.Ldloc, local);
            }

            il.Emit(OpCodes.Callvirt, interfaceMethod);

            if (interfaceMethod.ReturnType == typeof(void))
            {
                il.Emit(OpCodes.Call, CalleeBase_CompleteVoid);
            }
            else if (interfaceMethod.ReturnType == typeof(ValueTask))
            {
                il.Emit(OpCodes.Call, CalleeBase_WaitVoidTask);
            }
            else if (interfaceMethod.ReturnType == typeof(Task))
            {
                // WaitVoidTask(new ValueTask(res));
                il.Emit(OpCodes.Newobj, ValueTask_Ctor_Task);
                il.Emit(OpCodes.Call, CalleeBase_WaitVoidTask);
            }
            else if (interfaceMethod.ReturnType.IsGenericType)
            {
                var typeDef = interfaceMethod.ReturnType.GetGenericTypeDefinition();
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
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldloc, exLocal);
            il.Emit(OpCodes.Call, CalleeBase_Fail);

            il.Emit(OpCodes.Leave, retLabel);
        }
        il.EndExceptionBlock();

        il.MarkLabel(retLabel);
        il.Emit(OpCodes.Ret);

        return methodBuilder;
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
