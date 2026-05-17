using System.Diagnostics;
using Zarn.Protocol;
using Zarn.Protocol.Messages;

namespace Zarn.Invocation;

internal sealed class CreateInstanceDispatcher : IThreadPoolWorkItem
{
    public ConnectionContext? Connection { get; set; }

    public int TypeSlot { get; set; }

    public Type[] GenericArgs { get; set; } = [];

    public ObjectId InvokerId { get; set; }

    public void Execute()
    {
        Debug.Assert(Connection is { });
        var genericArgs = GenericArgs;
        var typeSlot = TypeSlot;
        var connection = Connection;
        var invokerId = InvokerId;

        GenericArgs = [];
        Connection = null;

        connection.Pools.Return(this);

        ObjectId instanceId = default;
        Exception? exception = null;

        try
        {
            var calleeFactory = connection.Pools.CalleeFactories[typeSlot - 1];
            var calleeType = genericArgs.Length > 0
                ? calleeFactory.InterfaceType.MakeGenericType(genericArgs)
                : calleeFactory.InterfaceType;

            var instance = connection.InstanceManager.Services.GetService(calleeType);
            if (instance is null)
            {
                exception = new InvalidOperationException("Requested service cannot be found");
            }
            else
            {
                var factory = genericArgs.Length == 0
                            ? calleeFactory.TryGetFactory(calleeFactory.InterfaceType)
                            : calleeFactory.TryGetFactory(calleeFactory.InterfaceType, genericArgs);
                Debug.Assert(factory is { });

                instanceId = connection.InstanceManager.Register(instance, factory);
            }
        }
        catch (Exception e)
        {
            exception = connection.Settings.WrapException(e);
        }

        connection.Dispatch(new CreateInstanceMessageResponse
        {
            InvokerId = invokerId,
            IsSuccess = exception is null,
            ObjectId = instanceId,
            Exception = exception,
        });
    }
}
