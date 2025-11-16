using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace StreamRpc.Protocol;

internal sealed class ExecuteRequestDispatcher : IThreadPoolWorkItem
{
    public ChunkedArrayPoolBufferWriter<byte>? MessageBuffer { get; set; }

    public ConnectionContext? Connection { get; set; }

    public void Execute()
    {
        Debug.Assert(MessageBuffer is { } && Connection is { });

        var messageBuffer = MessageBuffer;
        var connection = Connection;
        MessageBuffer = null;
        Connection = null;

        // Return instance ASAP to avoid unnecessary allocations.
        // It's safe to return instance at this point because instance properties are not accessed anymore
        connection.Pools.Return(this);

        Unsafe.SkipInit(out ExecuteRequestMessage message);

        long readerOffset = message.Deserialize(messageBuffer,
                                                connection.SerializationContext,
                                                connection.Pools,
                                                out var uncompressed);

        if (uncompressed is { })
        {
            connection.Pools.Return(messageBuffer);
            messageBuffer = uncompressed;
        }

        try
        {
            var descriptor = connection.InstanceManager.GetDescriptor(message.RemoteId);

            CalleeBase callee = descriptor.CalleeFactory.Get();
            callee.MethodSlot = message.MethodSlot;
            callee.GenericMethodArgs = message.GenericMethodArgs;
            callee.Factory = descriptor.CalleeFactory;
            callee.Connection = connection;
            callee.OperationId = message.OperationId;
            callee.Arguments = messageBuffer;
            callee.Impl = descriptor.Instance;
            callee.ReaderOffset = readerOffset;
            callee.Cts = connection.Pools.GetCts();

            if (!connection.CalleeOperations.Register(callee))
            {
                // TODO: handle case when caller does more than `MaxConcurrentOperations` calls at same time.
                // Currently this doesn't happen, and unlikely to happen in future, at least while people won't start
                // implementing own libs to talk to this
                Debug.Fail("not implemented");
                throw new NotImplementedException();
            }

            callee.Execute();
        }
        catch (Exception e)
        {
            Debug.Fail("Exception was unexpected at this point. Ex: " + e);
            // TODO: send Fail message
            Environment.FailFast(null, e);
        }
    }
}
