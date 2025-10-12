using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks.Sources;
using StreamRpc.Serialization;

namespace StreamRpc.Protocol;

internal delegate void SetToken<T>(ref ManualResetValueTaskSourceCore<T> source, short token);

internal abstract class InvokerOperation
{
    public abstract short Token { get; set; }

    public ConnectionContext? Context { get; set; }

    public abstract void Complete(BinarySerializationContext serializationContext, ref ReadOnlySequenceReader<byte> responseBody);

    public abstract void Complete(Exception e);

    protected static SetToken<T> GetSetTokenDelegate<T>()
    {
        var sourceType = typeof(ManualResetValueTaskSourceCore<T>);
        var field = sourceType.GetField("_version", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException("Cannot find ManualResetValueTaskSourceCore`1._version field");

        var method = new DynamicMethod("SetToken", typeof(void), [sourceType.MakeByRefType(), typeof(short)], true);

        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, field);
        il.Emit(OpCodes.Ret);

        return method.CreateDelegate<SetToken<T>>();
    }
}

internal sealed class InvokerOperation<T> : InvokerOperation, IValueTaskSource<T>
{
    private static readonly SetToken<T> s_setToken = GetSetTokenDelegate<T>();

    private ManualResetValueTaskSourceCore<T> _tcs;
    
    public override short Token
    {
        get => _tcs.Version;
        set => s_setToken.Invoke(ref _tcs, value);
    }

    public override void Complete(BinarySerializationContext serializationContext, ref ReadOnlySequenceReader<byte> responseBody)
    {
        var result = serializationContext.Deserialize<T>(ref responseBody);

        _tcs.SetResult(result);
    }

    public override void Complete(Exception e)
    {
        _tcs.SetException(e);
    }

    public T GetResult(short token)
    {
        try
        {
            return _tcs.GetResult(token);
        }
        finally
        {
            _tcs = default;
            var pools = Context?.Pools;
            Context = null;
            pools?.Return(this);
        }
    }

    public ValueTaskSourceStatus GetStatus(short token)
    {
        return _tcs.GetStatus(token);
    }

    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
    {
        _tcs.OnCompleted(continuation, state, token, flags);
    }
}

internal sealed class VoidInvokerOperation : InvokerOperation, IValueTaskSource
{
    private static readonly SetToken<object?> s_setToken = GetSetTokenDelegate<object?>();

    private ManualResetValueTaskSourceCore<object?> _tcs;

    public override short Token
    {
        get => _tcs.Version;
        set => s_setToken.Invoke(ref _tcs, value);
    }

    public override void Complete(BinarySerializationContext serializationContext, ref ReadOnlySequenceReader<byte> responseBody)
    {
        _tcs.SetResult(null);
    }

    public override void Complete(Exception e)
    {
        _tcs.SetException(e);
    }

    public void GetResult(short token)
    {
        try
        {
            _ = _tcs.GetResult(token);
        }
        finally
        {
            _tcs = default;
            var pools = Context?.Pools;
            Context = null;
            pools?.Return(this);
        }
    }

    public ValueTaskSourceStatus GetStatus(short token)
    {
        return _tcs.GetStatus(token);
    }

    public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
    {
        _tcs.OnCompleted(continuation, state, token, flags);
    }
}
