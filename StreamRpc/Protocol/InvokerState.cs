using System.Collections.Concurrent;
using System.Diagnostics;
using StreamRpc.Serialization;

namespace StreamRpc.Protocol;

internal sealed class InvokerState(Guid id, ConnectionContext context)
{
    public Guid Id { get; } = id;

    public ConnectionContext Connection { get; } = context;

    private readonly ConcurrentDictionary<short, InvokerOperation> _operations = [];
    private int _lastOpId;

    public void Complete(short opId, Exception exception)
    {
        _operations.Remove(opId).Complete(exception);
    }

    public void Complete(short opId, ref ReadOnlySequenceReader<byte> reader)
    {
        _operations.Remove(opId).Complete(Connection.SerializationContext, ref reader);
    }

    public InvokerOperation GetOperation(short id)
    {
        return _operations[id];
    }

    public InvokerOperation<T> CreateOperation<T>()
    {
        var op = Connection.Pools.GetInvokerOperation<T>();
        RegisterOperation(op);
        return op;
    }

    public VoidInvokerOperation CreateOperation()
    {
        var op = Connection.Pools.GetInvokerOperation();
        RegisterOperation(op);
        return op;
    }

    private void RegisterOperation(InvokerOperation operation)
    {
        var token = unchecked((short)Interlocked.Increment(ref _lastOpId));
        operation.Token = token;
        _operations.AddOrUpdate(token, operation, (key, value) =>
        {
            const string message = "Multiple operations with same id may not exist";
            Debug.Fail(message);
            throw new InvalidOperationException(message);
        });
    }
}
