using System.Diagnostics;
using StreamRpc.Serialization;

namespace StreamRpc.Protocol;

internal sealed class CalleeOperations(ConnectionContext connection, int maxConcurrentOperations)
{
    public BinarySerializationContext SerializationContext { get; } = connection.SerializationContext;

    public Pools Pools { get; } = connection.Pools;

    public ConnectionContext Connection { get; } = connection;

    private readonly CalleeBase?[] _callees = new CalleeBase?[maxConcurrentOperations];
    private readonly Lock _lock = new();

    public bool Register(CalleeBase callee)
    {
        lock (_lock)
        {
            var freeSlot = Array.IndexOf(_callees, null);
            if (freeSlot < 0)
            {
                return false;
            }

            _callees[freeSlot] = callee;
            return true;
        }
    }

    public CalleeBase? Find(OperationId id)
    {
        lock (_lock)
        {
            foreach (var c in _callees)
            {
                if (c is { } && c.OperationId == id)
                {
                    return c;
                }
            }
            return null;
        }
    }

    public void Remove(CalleeBase callee)
    {
        lock (_lock)
        {
            var slot = Array.IndexOf(_callees, callee);
            Debug.Assert(slot >= 0);
            _callees[slot] = null;
        }
    }

    public void CompleteResponse(CalleeBase callee, Exception? exception, ChunkedArrayPoolBufferWriter<byte>? returnValue)
    {
        Remove(callee);

        var options = exception is null ? ExecuteResponseOptions.Success : ExecuteResponseOptions.None;
        var header = Pools.GetWriter();

        header.Reserve(PackedInt.MaxSize);
        SerializationContext.Serialize(MessageType.ExecuteResponse, header);
        SerializationContext.Serialize(options, header);
        SerializationContext.Serialize(callee.OperationId, header);
        if (exception is not null)
        {
            SerializationContext.SerializeAny(exception, header);
        }

        Connection.Dispatch(header, returnValue);

        Pools.Return(Interlocked.Exchange(ref callee.Cts, null));

        callee.Factory.Return(callee);
    }
}
