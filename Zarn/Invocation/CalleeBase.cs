using System.Buffers;
using System.Diagnostics;
using Zarn.Collections;
using Zarn.Protocol;
using Zarn.Protocol.Messages;
using Zarn.Serialization;
using Zarn.TypeGeneration;

namespace Zarn.Invocation;

internal abstract class CalleeBase
{
    internal abstract object Impl { get; set; }

    protected CalleeBase() { }

    internal OperationId OperationId { get; set; }

    internal CancellationTokenSource? Cts;

    public ConnectionContext? Connection { get; set; }

    internal ICalleeFactory Factory { get; set; } = null!;

    public void Dispatch(ref SequenceReader<byte> reader, int methodSlot)
    {
        try
        {
            DispatchCore(ref reader, methodSlot);
        }
        catch (Exception ex)
        {
            // Exceptions generated from `Impl` are handled inside `InvokeStub#N` without wrapping.
            // Catch exception here to avoid overhead inside `ParseArgument` and `DispatchCore`
            Fail(new RpcInfrastructureException(ex));
        }
    }

    internal protected abstract void DispatchCore(ref SequenceReader<byte> argumentsReader, int methodSlot);

    internal protected T ParseArgument<T>(ref SequenceReader<byte> argumentsReader)
    {
        Debug.Assert(Connection is { });
        var value = Connection.SerializationContext.Deserialize<T>(ref argumentsReader);
        if (typeof(T) == typeof(CancellationToken))
        {
            if (((CancellationToken)(object)value!).IsCancellationRequested)
            {
                return value;
            }
            else
            {
                Debug.Assert(Cts is { }, "Cts is set within ConnectionContext during preparation");
                return (T)(object)Cts.Token;
            }
        }

        return value;
    }

    private ChunkedArrayPoolBufferWriter<byte> CreateResponseMessage(Exception? exception)
    {
        Debug.Assert(Connection is { });
        Connection.CalleeOperations.Remove(this);

        var options = ExecuteResponseOptions.None;
        if (exception is null)
        {
            options |= ExecuteResponseOptions.Success;
        }
        if (Connection.Pools.CompressionProvider is { })
        {
            options |= ExecuteResponseOptions.Compressed;
        }

        var header = Connection.Pools.GetWriter();

        header.Reserve(PackedInt.MaxSize);
        Connection.SerializationContext.Serialize(MessageType.ExecuteResponse, header);
        Connection.SerializationContext.Serialize(options, header);
        Connection.SerializationContext.Serialize(OperationId, header);
        if (exception is not null)
        {
            if (Connection.Pools.TryGetCompressor() is { } compressor)
            {
                var compressedWriter = Connection.Pools.GetWriter();
                Connection.SerializationContext.SerializeAny(exception, compressedWriter);

                compressor.Compress(compressedWriter.GetSequence(), header);

                Connection.Pools.Return(compressedWriter);
                Connection.Pools.Return(compressor);
            }
            else
            {
                Connection.SerializationContext.SerializeAny(exception, header);
            }
        }

        return header;
    }

    private void SendResponse(ChunkedArrayPoolBufferWriter<byte> header)
    {
        Debug.Assert(Connection is { });

        Connection.Dispatch(header);

        Connection.Pools.Return(Interlocked.Exchange(ref Cts, null));
        Connection = null;
        Factory.Return(this);
    }


    internal protected GenericMethodInvokeTrampoline? TryFindTrampoline(ref SingleLinkedListNode<GenericMethodInvokeTrampoline>? head,
                                                                        ReadOnlySpan<Type> genericArgs)
    {
        var current = head;
        while (current is { })
        {
            if (current.Value.Matches(genericArgs))
            {
                return current.Value;
            }

            current = Volatile.Read(ref current.Next);
        }

        return null;
    }

    internal protected GenericMethodInvokeTrampoline CreateTrampoline(ref SingleLinkedListNode<GenericMethodInvokeTrampoline>? head,
                                                                      Type trampolineType)
    {
        var trampoline = (GenericMethodInvokeTrampoline)Activator.CreateInstance(trampolineType)!;
        var node = new SingleLinkedListNode<GenericMethodInvokeTrampoline>(trampoline);

        var actual = Interlocked.CompareExchange(ref head, node, null);
        if (actual is null)
        {
            // head was set
            return trampoline;
        }

        var current = head;
        while (true)
        {
            actual = Interlocked.CompareExchange(ref current.Next, node, null);
            if (actual == null)
            {
                // value was set
                return trampoline;
            }
            else if (actual.Value.GetType() == trampolineType)
            {
                // other trampoline with same type was set
                return actual.Value;
            }

            current = Volatile.Read(ref current.Next);
        }
    }

    internal protected void Fail(Exception e)
    {
        Debug.Assert(Connection is { });
        FailCore(Connection.Settings.WrapException(e));
    }

    private void FailCore(Exception e)
    {
        SendResponse(CreateResponseMessage(e));
    }

    internal protected void CompleteVoid()
    {
        SendResponse(CreateResponseMessage(null));
    }

    internal protected void Complete<T>(T value)
    {
        Debug.Assert(Connection is { });
        var message = CreateResponseMessage(null);

        if (Connection.Pools.TryGetCompressor() is { } compressor)
        {
            var compressedWriter = Connection.Pools.GetWriter();
            Connection.SerializationContext.Serialize(value, compressedWriter);

            compressor.Compress(compressedWriter.GetSequence(), message);

            Connection.Pools.Return(compressedWriter);
            Connection.Pools.Return(compressor);
        }
        else
        {
            Connection.SerializationContext.Serialize(value, message);
        }

        SendResponse(message);
    }

    internal protected void WaitVoidTask(ValueTask valueTask)
    {
        Debug.Assert(Connection is { });
        if (valueTask.IsCompleted)
        {
            CompleteVoidTask(valueTask);
        }
        else
        {
            var worker = Connection.Pools.GetOnCompletedWorker();
            worker.Task = valueTask;
            worker.Callee = this;
            valueTask.GetAwaiter().UnsafeOnCompleted(worker.OnCompleted);
        }
    }

    internal void CompleteVoidTask(ValueTask valueTask)
    {
        Debug.Assert(valueTask.IsCompleted);
        try
        {
            valueTask.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Fail(ex);
            return;
        }

        CompleteVoid();
    }

    internal void CompleteTask<T>(ValueTask<T> valueTask)
    {
        Debug.Assert(valueTask.IsCompleted);
        T result;
        try
        {
            result = valueTask.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Fail(ex);
            return;
        }

        Complete(result);
    }

    internal protected void WaitTask<T>(ValueTask<T> valueTask)
    {
        Debug.Assert(Connection is { });
        if (valueTask.IsCompleted)
        {
            CompleteTask(valueTask);
        }
        else
        {
            var worker = Connection.Pools.GetOnCompletedWorker<T>();
            worker.Task = valueTask;
            worker.Callee = this;
            valueTask.GetAwaiter().UnsafeOnCompleted(worker.OnCompleted);
        }
    }
}
