using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection.Emit;
using Zarn.TypeGeneration;

namespace Zarn.Protocol;

internal interface IInstanceManager
{
    ValueTask<ObjectId> CreateInstance(int typeSlot, Type[] genericArgs);

    ValueTask<ObjectId> GetEnumerator(ObjectId enumerableId, Type genArg);

    ValueTask<ObjectId> GetAsyncEnumerator(ObjectId enumerableId, Type genArg);

    ValueTask CancelAsyncEnumerator(ObjectId enumeratorId);

    ValueTask RemoveObject(ObjectId id);
}

internal readonly record struct InstanceDescriptor(ObjectId Id,
                                                   object Instance,
                                                   ICalleeFactory CalleeFactory,
                                                   CancellationTokenSource? Cts);

internal sealed class InstanceManager : IInstanceManager
{
    private readonly ConcurrentDictionary<ObjectId, InstanceDescriptor> _descriptors = [];
    private readonly ConcurrentDictionary<ObjectId, InvokerState> _invokers = [];
    private readonly ConcurrentDictionary<Type, Func<object, object>> _getEnumerator = [];
    private readonly ConcurrentDictionary<Type, Func<object, CancellationToken, object>> _getAsyncEnumerator = [];
    private readonly EnumeratorCalleeFactory _enumeratorCalleeFactory = new();
    private readonly Pools _pools;
    private readonly ConnectionContext _connection;
    private readonly IServiceProvider _services;

    public InstanceManager(ConnectionContext connection, IServiceProvider services)
    {
        _connection = connection;
        _pools = connection.Pools;
        _services = services;
        _descriptors[CommunicationServices.InstanceManager] = new InstanceDescriptor(CommunicationServices.InstanceManager, this, GetCalleeFactory(typeof(IInstanceManager)), null);
    }

    public InstanceDescriptor GetDescriptor(ObjectId id) => _descriptors[id];

    public ObjectId Register(object instance, ICalleeFactory factory)
    {
        return Register(instance, factory, null);
    }

    public ObjectId Register(object instance, ICalleeFactory factory, CancellationTokenSource? cts)
    {
        var remoteId = ObjectId.GenObjectId();
        while (!_descriptors.TryAdd(remoteId, new InstanceDescriptor(remoteId, instance, factory, cts)))
        {
            remoteId = ObjectId.GenObjectId();
        }

        return remoteId;
    }

    public ValueTask<ObjectId> CreateInstance(int typeSlot, Type[] genericArgs)
    {
        var calleeFactory = _pools.CalleeFactories[typeSlot - 1];
        var calleeType = genericArgs.Length > 0
            ? calleeFactory.InterfaceType.MakeGenericType(genericArgs)
            : calleeFactory.InterfaceType;

        var instance = _services.GetService(calleeType)
            ?? throw ThrowHelper.Unreachable; // service must be registered

        var factory = genericArgs.Length == 0
                    ? calleeFactory.TryGetFactory(calleeFactory.InterfaceType)
                    : calleeFactory.TryGetFactory(calleeFactory.InterfaceType, genericArgs);
        Debug.Assert(factory is { });

        var id = Register(instance, factory);
        return ValueTask.FromResult(id);
    }

    public InvokerBase GetInvoker(Type invokerType, ObjectId id, bool finalizable)
    {
        int typeSlot = -1;
        Type interfaceType = invokerType.IsConstructedGenericType
                                ? invokerType.GetGenericTypeDefinition()
                                : invokerType;

        var factories = finalizable ? _pools.ReverseCalleeFactories : _pools.InvokerFactories;
        for (int i = 0; i < factories.Length; i++)
        {
            if (factories[i].InterfaceType == interfaceType)
            {
                typeSlot = i;
                break;
            }
        }

        if (typeSlot < 0)
        {
            throw new KeyNotFoundException("Unable to find implementation for type: " + invokerType);
        }

        Type[] genericArgs = [];
        InvokerBase invoker;
        if (invokerType.IsConstructedGenericType)
        {
            genericArgs = invokerType.GetGenericArguments();
            invoker = factories[typeSlot].GetInvoker(finalizable, genericArgs);
        }
        else
        {
            invoker = factories[typeSlot].GetInvoker(finalizable);
        }

        invoker.State = new InvokerState(_connection, typeSlot + 1, genericArgs)
        {
            Id = id,
        };

        RegisterInvoker(invoker);

        return invoker;
    }

    public InvokerState GetInvokerState(ObjectId id) => _invokers[id];

    public void RegisterInvoker(InvokerBase invoker)
    {
        _invokers.AddOrUpdate(invoker.State.Id, invoker.State, (key, value) =>
        {
            const string message = "Multiple invokers with same id may not exist";
            Debug.Fail(message);
            throw new InvalidOperationException(message);
        });
    }

    public ValueTask<ObjectId> GetAsyncEnumerator(ObjectId enumerableId, Type genArg)
    {
        var enumerable = _descriptors[enumerableId].Instance;
        var cts = new CancellationTokenSource();
        object enumerator;
        try
        {
            enumerator = _getAsyncEnumerator.GetOrAdd(genArg, CreateInvokeGetAsyncEnumerator).Invoke(enumerable, cts.Token);
        }
        catch
        {
            cts.Dispose();
            throw;
        }

        var id = Register(enumerator, _enumeratorCalleeFactory.GetFactory(genArg), cts);
        return ValueTask.FromResult(id);
    }

    public ValueTask<ObjectId> GetEnumerator(ObjectId enumerableId, Type genArg)
    {
        var enumerable = _descriptors[enumerableId].Instance;
        var enumerator = _getEnumerator.GetOrAdd(genArg, CreateInvokeGetEnumerator).Invoke(enumerable);

        var id = Register(enumerator, _enumeratorCalleeFactory.GetFactory(genArg));
        return ValueTask.FromResult(id);
    }

    public ValueTask CancelAsyncEnumerator(ObjectId enumeratorId)
    {
        var cts = _descriptors[enumeratorId].Cts;
        Debug.Assert(cts is { });
        cts.Cancel();

        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveObject(ObjectId id)
    {
        if (_descriptors.TryRemove(id, out var descriptor))
        {
            descriptor.Cts?.Dispose();
        }
        else
        {
            Debug.Fail("Object may not be removed twice");
        }

        return ValueTask.CompletedTask;
    }

    public ICalleeFactory GetCalleeFactory(Type calleeType)
    {
        if (calleeType.IsConstructedGenericType)
        {
            var genArgs = calleeType.GetGenericArguments();
            var genDef = calleeType.GetGenericTypeDefinition();

            foreach (var factory in _pools.ReverseInvokerFactories)
            {
                if (factory.TryGetFactory(genDef, genArgs) is { } f)
                {
                    return f;
                }
            }
        }
        else
        {
            foreach (var factory in _pools.ReverseInvokerFactories)
            {
                if (factory.TryGetFactory(calleeType) is { } f)
                {
                    return f;
                }
            }
        }


        throw ThrowHelper.Unreachable;
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
