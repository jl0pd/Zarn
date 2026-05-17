using System.Diagnostics;
using System.Runtime.CompilerServices;
using Zarn.Collections;
using Zarn.Protocol;
using Zarn.Protocol.Messages;

namespace Zarn.Invocation;

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

        ChunkedArrayPoolBufferWriter<byte>? uncompressed = null;
        try
        {
            var reader = message.Deserialize(messageBuffer,
                                             connection.SerializationContext,
                                             connection.Pools,
                                             out uncompressed);

            var descriptor = connection.InstanceManager.GetDescriptor(message.RemoteId);

            CalleeBase callee = descriptor.CalleeFactory.Get();
            callee.Factory = descriptor.CalleeFactory;
            callee.Connection = connection;
            callee.OperationId = message.OperationId;
            callee.Impl = descriptor.Instance;
            callee.Cts = connection.Pools.GetCts();

            if (!connection.CalleeOperations.Register(callee))
            {
                // TODO: handle case when caller does more than `MaxConcurrentOperations` calls at same time.
                // Currently this doesn't happen, and unlikely to happen in future, at least while people won't start
                // implementing own libs to talk to this
                Debug.Fail("not implemented");
                throw new NotImplementedException();
            }

            callee.Dispatch(ref reader, message.MethodSlot - 1);

        }
        catch (Exception e)
        {
            connection.Fail(e);
        }

        connection.Pools.Return(messageBuffer);
        connection.Pools.Return(uncompressed);
    }
}
