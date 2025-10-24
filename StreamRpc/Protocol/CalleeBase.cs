using System.Buffers;
using System.Diagnostics;
using StreamRpc.Serialization;

namespace StreamRpc.Protocol;

internal abstract class CalleeBase : IThreadPoolWorkItem
{
    internal abstract Type ImplementedInterface { get; }

    internal abstract object Impl { get; set; }

    protected CalleeBase() { }

    internal OperationId OperationId { get; set; }

    internal BinarySerializationContext SerializationContext => Callees.SerializationContext;

    internal ChunkedArrayPoolBufferWriter<byte>? Arguments { get; set; }

    internal long ReaderOffset { get; set; }

    internal CancellationTokenSource? Cts;
    internal CalleesState Callees { get; set; } = null!;

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
        var value = SerializationContext.Deserialize<T>(ref argumentsReader);
        if (argumentsReader.Remaining == 0)
        {
            Callees.Pools.Return(Arguments);
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

    internal protected void Fail(Exception e)
    {
        switch (Callees.Connection.Settings.UnhandledExceptionPropagationBehavior)
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
        Callees.CompleteResponse(this, e, null);
    }

    internal protected void CompleteVoid()
    {
        Callees.CompleteResponse(this, null, null);
    }

    internal protected void Complete<T>(T value)
    {
        var writer = Callees.Pools.GetWriter();
        SerializationContext.Serialize(value, writer);
        Callees.CompleteResponse(this, null, writer);
    }

    internal protected void WaitVoidTask(ValueTask valueTask)
    {
        if (valueTask.IsCompleted)
        {
            CompleteVoidTask(valueTask);
        }
        else
        {
            var worker = Callees.Pools.GetOnCompletedWorker();
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
        if (valueTask.IsCompleted)
        {
            CompleteTask(valueTask);
        }
        else
        {
            var worker = Callees.Pools.GetOnCompletedWorker<T>();
            worker.Task = valueTask;
            worker.Callee = this;
            valueTask.GetAwaiter().UnsafeOnCompleted(worker.OnCompleted);
        }
    }
}
