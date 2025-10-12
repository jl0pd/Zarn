using System.Buffers;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using StreamRpc.Protocol;
using StreamRpc.Serialization;

namespace StreamRpc.TypeGeneration;

internal static class InvokerImplementer
{
    private static readonly ConcurrentDictionary<Type, Lazy<Type>> s_types = [];
    private static readonly ModuleBuilder s_module;
    private static readonly ConstructorInfo InvokerBase_ctor;
    private static readonly MethodInfo InvokerBase_BeginCall;
    private static readonly MethodInfo InvokerBase_CompleteCallT;
    private static readonly MethodInfo InvokerBase_CompleteVoidCall;
    private static readonly MethodInfo InvokerBase_SynchronousWaitValueResultT;
    private static readonly MethodInfo InvokerBase_SynchronousWaitVoidValueResult;
    private static readonly MethodInfo InvokerBase_SynchronousWaitResultT;
    private static readonly MethodInfo InvokerBase_SynchronousWaitVoidResult;
    private static readonly MethodInfo InvokerBase_GetMethodSlot;
    private static readonly MethodInfo InvokerBase_GetTypeSlot;
    private static readonly MethodInfo InvokerBase_Get_SerializationContext;
    private static readonly MethodInfo SerializationContext_SerializeT;
    private static readonly MethodInfo ValueTask_AsTask;

    static InvokerImplementer()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName()
        {
            Name = "Rpc assembly"
        }, AssemblyBuilderAccess.Run);

        s_module = assembly.DefineDynamicModule("<module>");

        ImplementerCommon.CreateIgnoreAccessChecks(s_module);

        var typeInfo = typeof(InvokerBase).GetTypeInfo();
        InvokerBase_ctor = typeInfo.DeclaredConstructors.Single();
        InvokerBase_BeginCall = typeInfo.GetDeclaredMethod(nameof(InvokerBase.BeginCall))!;
        InvokerBase_CompleteCallT = typeInfo.GetDeclaredMethod(nameof(InvokerBase.CompleteCall))!;
        InvokerBase_CompleteVoidCall = typeInfo.GetDeclaredMethod(nameof(InvokerBase.CompleteVoidCall))!;
        InvokerBase_SynchronousWaitValueResultT = typeInfo.GetDeclaredMethod(nameof(InvokerBase.SynchronousWaitValueResult))!;
        InvokerBase_SynchronousWaitVoidValueResult = typeInfo.GetDeclaredMethod(nameof(InvokerBase.SynchronousWaitVoidValueResult))!;
        InvokerBase_SynchronousWaitResultT = typeInfo.GetDeclaredMethod(nameof(InvokerBase.SynchronousWaitResult))!;
        InvokerBase_SynchronousWaitVoidResult = typeInfo.GetDeclaredMethod(nameof(InvokerBase.SynchronousWaitVoidResult))!;
        InvokerBase_GetMethodSlot = typeInfo.GetDeclaredMethod(nameof(InvokerBase.GetMethodSlot))!;
        InvokerBase_GetTypeSlot = typeInfo.GetDeclaredProperty(nameof(InvokerBase.TypeSlot))!.GetGetMethod(true)!;
        InvokerBase_Get_SerializationContext = typeInfo.GetDeclaredProperty(nameof(InvokerBase.SerializationContext))!.GetGetMethod(true)!;
        SerializationContext_SerializeT = typeof(BinarySerializationContext).GetMethod(nameof(BinarySerializationContext.Serialize))!;
        ValueTask_AsTask = typeof(ValueTask).GetMethod(nameof(ValueTask.AsTask))!;
    }

    public static Type GetImplementation(Type interfaceType)
    {
        return s_types.GetOrAdd(interfaceType, t => new Lazy<Type>(() => ImplementType(t))).Value;
    }

    private static Type ImplementType(Type interfaceType)
    {
        var typeBuilder = s_module.DefineType(interfaceType.Name + "Impl", TypeAttributes.Sealed);
        typeBuilder.SetParent(typeof(InvokerBase));
        typeBuilder.AddInterfaceImplementation(interfaceType);

        ImplementerCommon.DefineCtor(typeBuilder, InvokerBase_ctor);
        ImplementerCommon.DefineImplementedInterfaceProp(typeBuilder, interfaceType);

        var methods = interfaceType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
        for (int i = 0; i < methods.Length; i++)
        {
            ImplementMethod(typeBuilder, methods[i], i);
        }

        return typeBuilder.CreateType();
    }

    private static void ImplementMethod(TypeBuilder typeBuilder, MethodInfo interfaceMethod, int methodIndex)
    {
        var method = typeBuilder.DefineMethod(
                interfaceMethod.Name,
                MethodAttributes.Public | MethodAttributes.NewSlot | MethodAttributes.Virtual | MethodAttributes.Final,
                CallingConventions.HasThis,
                interfaceMethod.ReturnType,
                interfaceMethod.GetParameters().Select(p => p.ParameterType).ToArray());

        var methodInfoField = typeBuilder.DefineField("_methodInfo#" + methodIndex, typeof(MethodInfo), FieldAttributes.Private);

        var returnValueType = ImplementerCommon.GetValueType(interfaceMethod.ReturnType);

        var il = method.GetILGenerator();

        var opIdLocal = il.DeclareLocal(typeof(OperationId));
        var ctLocal = il.DeclareLocal(typeof(CancellationToken));
        var bufferWriterLocal = il.DeclareLocal(typeof(IBufferWriter<byte>));

        // TODO: read from parameters
        // var cancellationToken = default;
        il.Emit(OpCodes.Ldloca, ctLocal);
        il.Emit(OpCodes.Initobj, typeof(CancellationToken));

        // var bufferWriter = base.BeginCall(out OperationId, cancellationToken);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldloca, opIdLocal);
        il.Emit(OpCodes.Ldloc, ctLocal);
        il.Emit(OpCodes.Call, InvokerBase_BeginCall);
        il.Emit(OpCodes.Stloc, bufferWriterLocal);

        // base.SerializationContext.Serialize(base.TypeSlot, bufferWriter);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, InvokerBase_Get_SerializationContext);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, InvokerBase_GetTypeSlot);
        il.Emit(OpCodes.Ldloc, bufferWriterLocal);
        il.Emit(OpCodes.Call, SerializationContext_SerializeT.MakeGenericMethod(typeof(int)));

        if (interfaceMethod.DeclaringType!.IsGenericType)
        {
            throw new NotImplementedException();
        }

        // base.SerializationContext.Serialize(base.GetMethodSlot(_methodInfo ??= methodof(interfaceMethod)), bufferWriter);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, InvokerBase_Get_SerializationContext);
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
        il.Emit(OpCodes.Ldloc, bufferWriterLocal);
        il.Emit(OpCodes.Call, SerializationContext_SerializeT.MakeGenericMethod(typeof(int)));

        if (method.IsGenericMethod)
        {
            throw new NotImplementedException();
        }

        var parameters = interfaceMethod.GetParameters();
        for (int i = 0; i < parameters.Length; i++)
        {
            // base.SerializationContext.Serialize(parameterN, bufferWriter);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, InvokerBase_Get_SerializationContext);
            il.Emit(OpCodes.Ldarg, i + 1);
            il.Emit(OpCodes.Ldloc, bufferWriterLocal);
            il.Emit(OpCodes.Call, SerializationContext_SerializeT.MakeGenericMethod(parameters[i].ParameterType));
        }

        // var task = base.CompleteCall<retType>(bufferWriter, MessageOptions.None, opId, cancellationToken);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldloc, bufferWriterLocal);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ldloc, opIdLocal);
        il.Emit(OpCodes.Ldloc, ctLocal);
        if (returnValueType == typeof(void))
        {
            il.Emit(OpCodes.Call, InvokerBase_CompleteVoidCall);
        }
        else
        {
            il.Emit(OpCodes.Call, InvokerBase_CompleteCallT.MakeGenericMethod(returnValueType));
        }

        if (interfaceMethod.ReturnType == typeof(void))
        {
            // SynchronousWaitVoidResult(task);
            // return;

            il.Emit(OpCodes.Call, InvokerBase_SynchronousWaitVoidValueResult);
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
                il.Emit(OpCodes.Call, InvokerBase_SynchronousWaitValueResultT.MakeGenericMethod(interfaceMethod.ReturnType));
            }
        }
        else
        {
            // return SynchronousWaitResult<retType>(task);
            il.Emit(OpCodes.Call, InvokerBase_SynchronousWaitValueResultT.MakeGenericMethod(interfaceMethod.ReturnType));
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
