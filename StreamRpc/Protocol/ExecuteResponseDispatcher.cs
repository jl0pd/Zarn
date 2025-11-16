using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace StreamRpc.Protocol;

internal sealed class ExecuteResponseDispatcher : IThreadPoolWorkItem
{
    public ConnectionContext? Connection { get; set; }

    public ChunkedArrayPoolBufferWriter<byte>? MessageBuffer { get; set; }

    public void Execute()
    {
        Debug.Assert(Connection is { } && MessageBuffer is { });

        var messageBuffer = MessageBuffer;
        var connection = Connection;
        MessageBuffer = null;
        Connection = null;

        // Return instance ASAP to avoid unnecessary allocations.
        // It's safe to return instance at this point because instance properties are not accessed anymore
        connection.Pools.Return(this);

        Unsafe.SkipInit(out ExecuteResponseMessage message);

        var reader = message.Deserialize(messageBuffer,
                                         connection.SerializationContext,
                                         connection.Pools,
                                         out var uncompressed);

        var invoker = connection.InstanceManager.GetInvokerState(message.OperationId.Target);

        if (message.Options.HasFlag(ExecuteResponseOptions.Success))
        {
            invoker.Complete(message.OperationId.Id, ref reader);
        }
        else
        {
            var ex = (Exception?)connection.SerializationContext.DeserializeAny(ref reader);
            Debug.Assert(ex is { });
            invoker.Complete(message.OperationId.Id, ex);
        }

        Debug.Assert(reader.End);

        connection.Pools.Return(messageBuffer);
        connection.Pools.Return(uncompressed);
    }
}
