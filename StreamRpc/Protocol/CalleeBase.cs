using System.Buffers;
using System.Diagnostics;
using StreamRpc.Serialization;
using StreamRpc.TypeGeneration;

namespace StreamRpc.Protocol;

internal abstract class CalleeBase
{
    internal abstract object Impl { get; set; }

    protected CalleeBase() { }

    internal OperationId OperationId { get; set; }

    internal ChunkedArrayPoolBufferWriter<byte>? Arguments { get; set; }

    internal long ReaderOffset { get; set; }

    internal CancellationTokenSource? Cts;

    public ConnectionContext? Connection { get; set; }

    internal ICalleeFactory Factory { get; set; } = null!;

    internal int MethodSlot { get; set; }

    internal Type[]? GenericMethodArgs { get; set; }

    public void Execute()
    {
        try
        {
            Debug.Assert(Arguments is { });
            var reader = Arguments.GetReader();
            reader.Advance(ReaderOffset);
            DispatchCore(ref reader, MethodSlot - 1);
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
        if (argumentsReader.Remaining == 0)
        {
            Connection.Pools.Return(Arguments);
            Arguments = null;
        }
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

        var options = exception is null ? ExecuteResponseOptions.Success : ExecuteResponseOptions.None;
        var header = Connection.Pools.GetWriter();

        header.Reserve(PackedInt.MaxSize);
        Connection.SerializationContext.Serialize(MessageType.ExecuteResponse, header);
        Connection.SerializationContext.Serialize(options, header);
        Connection.SerializationContext.Serialize(OperationId, header);
        if (exception is not null)
        {
            Connection.SerializationContext.SerializeAny(exception, header);
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

    internal protected void Fail(Exception e)
    {
        Debug.Assert(Connection is { });
        switch (Connection.Settings.UnhandledExceptionPropagationBehavior)
        {
            case UnhandledExceptionPropagationBehavior.Hidden:
                FailCore(new UnhandledRpcException("Internal error has occurred"));
                break;
            case UnhandledExceptionPropagationBehavior.WrapToString:
                FailCore(e as UnhandledRpcException ?? new UnhandledRpcException(e.ToString()));
                break;
            case UnhandledExceptionPropagationBehavior.TransparentWrap:
                FailCore(e as UnhandledRpcException
                    ?? new UnhandledRpcException("Unhandled exception has occurred. See InnerException for more details", e));
                break;
            case UnhandledExceptionPropagationBehavior.TransparentNoWrap:
                FailCore(e);
                break;
        }
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
        Connection.SerializationContext.Serialize(value, message);
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
