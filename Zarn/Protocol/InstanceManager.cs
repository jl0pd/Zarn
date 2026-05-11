using System.Collections.Concurrent;
using System.Diagnostics;
using Zarn.Invocation;
using Zarn.TypeGeneration;

namespace Zarn.Protocol;

internal readonly record struct InstanceDescriptor(ObjectId Id,
                                                   object Instance,
                                                   ICalleeFactory CalleeFactory,
                                                   CancellationTokenSource? Cts);

internal sealed class InstanceManager(ConnectionContext connection, IServiceProvider services)
{
    private readonly ConcurrentDictionary<ObjectId, InstanceDescriptor> _descriptors = [];
    private readonly ConcurrentDictionary<ObjectId, InvokerState> _invokers = [];
    private readonly Pools _pools = connection.Pools;

    public IServiceProvider Services => services;

    public InstanceDescriptor GetDescriptor(ObjectId id) => _descriptors[id];

    public ObjectId Register(object instance, ICalleeFactory factory)
    {
        return Register(instance, factory, null);
    }

    public ObjectId Register(object instance, ICalleeFactory factory, CancellationTokenSource? cts)
    {
        var remoteId = connection.GenObjectId();

        _descriptors.AddOrUpdate(remoteId, new InstanceDescriptor(remoteId, instance, factory, cts), (k, v) =>
        {
            const string message = "Two objects with same id cannot exist";
            Debug.Fail(message);
            throw new Exception(message);
        });

        return remoteId;
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

        invoker.State = new CommonInvokerState(connection, typeSlot + 1, genericArgs)
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

    public void RemoveObject(ObjectId id)
    {
        if (_descriptors.TryRemove(id, out var descriptor))
        {
            descriptor.Cts?.Dispose();
        }
        else
        {
            Debug.Fail("Object may not be removed twice");
        }
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
}
