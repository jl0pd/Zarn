using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using StreamRpc.Serialization;

namespace StreamRpc.Protocol;

internal abstract class InvokerBase
{
    internal ConnectionContext Connection { get; set; } = null!;

    internal protected abstract Type ImplementedInterface { get; }

    internal Guid Id { get; set; }

    internal MethodInfo?[] MethodSlots { get; set; } = [];

    internal protected BinarySerializationContext SerializationContext => Connection.SerializationContext;

    private int _opId;

    protected internal int TypeSlot { get; set; }

    internal protected int GetMethodSlot(MethodInfo method)
    {
        Debug.Assert(method.IsGenericMethodDefinition || !method.IsGenericMethod);
        var idx = Array.IndexOf(MethodSlots, method);
        if (idx < 0)
        {
            throw new InvalidOperationException("Server does not support given method: " + method);
        }

        return idx + 1;
    }

    internal protected IBufferWriter<byte> BeginCall(out OperationId operationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var writer = Connection.Pools.GetWriter();
        writer.Reserve(PackedInt.MaxSize);
        SerializationContext.Serialize(MessageOptions.None, writer);
        SerializationContext.Serialize(MessageType.ExecuteRequest, writer);

        // TODO: handle overflow & free id list
        operationId = new OperationId(Id, (short)Interlocked.Increment(ref _opId));
        SerializationContext.Serialize(operationId, writer);

        return writer;
    }

    internal protected ValueTask<T> CompleteCall<T>(IBufferWriter<byte> writer, MessageOptions options, OperationId operationId, CancellationToken cancellationToken)
    {
        var chunkedWriter = (ChunkedArrayPoolBufferWriter<byte>)writer;

        var task = new ValueTask<T>(Connection.StartInvokerOperation<T>(operationId), operationId.Id);
        Connection.Dispatch(options, chunkedWriter, null);
        return task;
    }

    internal protected ValueTask CompleteVoidCall(IBufferWriter<byte> writer, MessageOptions options, OperationId operationId, CancellationToken cancellationToken)
    {
        var chunkedWriter = (ChunkedArrayPoolBufferWriter<byte>)writer;

        var task = new ValueTask(Connection.StartVoidInvokerOperation(operationId), operationId.Id);
        Connection.Dispatch(options, chunkedWriter, null);
        return task;
    }

    internal protected static T SynchronousWaitValueResult<T>(ValueTask<T> task)
    {
        var awaiter = task.GetAwaiter();
        if (awaiter.IsCompleted)
        {
            return awaiter.GetResult();
        }
        else
        {
            return task.AsTask().GetAwaiter().GetResult();
        }
    }

    internal protected static void SynchronousWaitVoidValueResult(ValueTask task)
    {
        var awaiter = task.GetAwaiter();
        if (awaiter.IsCompleted)
        {
            awaiter.GetResult();
        }
        else
        {
            task.AsTask().GetAwaiter().GetResult();
        }
    }

    internal protected static T SynchronousWaitResult<T>(Task<T> task)
    {
        return task.GetAwaiter().GetResult();
    }

    internal protected static void SynchronousWaitVoidResult(Task task)
    {
        task.GetAwaiter().GetResult();
    }
}
