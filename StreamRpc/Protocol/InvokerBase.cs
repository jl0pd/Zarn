using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using StreamRpc.Serialization;

namespace StreamRpc.Protocol;

internal abstract class InvokerBase
{
    internal protected abstract Type ImplementedInterface { get; }

    internal InvokerState State
    {
        get => _state;
        set
        {
            _state = value;
            SerializationContext = value.Connection.SerializationContext;
        }
    }
    private InvokerState _state = null!;

    internal MethodInfo?[] MethodSlots { get; set; } = [];

    internal protected BinarySerializationContext SerializationContext { get; private set; } = null!;

    internal protected int TypeSlot { get; set; }

    internal protected int GetMethodSlot(MethodInfo method)
    {
        Debug.Assert(method.IsGenericMethodDefinition || !method.IsGenericMethod);
        var idx = Array.IndexOf(MethodSlots, method);
        if (idx < 0)
        {
            throw new InvalidOperationException("Other side does not support given method: " + method);
        }

        return idx + 1;
    }

    internal protected IBufferWriter<byte> BeginCall<T>(out InvokerOperation<T> operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var writer = State.Connection.Pools.GetWriter();
        writer.Reserve(PackedInt.MaxSize);
        SerializationContext.Serialize(MessageOptions.None, writer);
        SerializationContext.Serialize(MessageType.ExecuteRequest, writer);

        operation = State.CreateOperation<T>();
        SerializationContext.Serialize(new OperationId(State.Id, operation.Token), writer);

        return writer;
    }

    internal protected IBufferWriter<byte> BeginVoidCall(out VoidInvokerOperation operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var writer = State.Connection.Pools.GetWriter();
        writer.Reserve(PackedInt.MaxSize);
        SerializationContext.Serialize(MessageOptions.None, writer);
        SerializationContext.Serialize(MessageType.ExecuteRequest, writer);

        operation = State.CreateOperation();
        SerializationContext.Serialize(new OperationId(State.Id, operation.Token), writer);

        return writer;
    }

    internal protected ValueTask<T> CompleteCall<T>(IBufferWriter<byte> writer, MessageOptions options, InvokerOperation<T> operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var chunkedWriter = (ChunkedArrayPoolBufferWriter<byte>)writer;

        var task = new ValueTask<T>(operation, operation.Token);
        State.Connection.Dispatch(options, chunkedWriter, null);
        return task;
    }

    internal protected ValueTask CompleteVoidCall(IBufferWriter<byte> writer, MessageOptions options, VoidInvokerOperation operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var chunkedWriter = (ChunkedArrayPoolBufferWriter<byte>)writer;

        var task = new ValueTask(operation, operation.Token);
        State.Connection.Dispatch(options, chunkedWriter, null);
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
}
