using System.Buffers;
using System.Diagnostics;

namespace StreamRpc.Protocol;

internal sealed class InvokerState(Guid id, ConnectionContext context, int maxConcurrentOperations, SemaphoreSlim semaphore)
{
    public Guid Id { get; } = id;

    public ConnectionContext Connection { get; } = context;

    private int _allocated = 0;
    private readonly short[] _operationIds = new short[maxConcurrentOperations];
    private readonly InvokerOperation?[] _operations = new InvokerOperation[maxConcurrentOperations];
    private short _lastOpId;
    private readonly Lock _lock = new();

    public void Complete(short opId, Exception exception)
    {
        Remove(opId).Complete(exception);
    }

    public void Complete(short opId, ref SequenceReader<byte> reader)
    {
        Remove(opId).Complete(ref reader);
    }

    private InvokerOperation Remove(short opId)
    {
        InvokerOperation? op = null;
        using (_lock.EnterScope())
        {
            var ops = _operations;
            for (int i = 0; i < ops.Length; i++)
            {
                op = ops[i];
                if (op is { } && op.Token == opId)
                {
                    ops[i] = null;
                    _allocated--;
                    break;
                }
            }
        }

        Debug.Assert(op is { });
        semaphore.Release();
        return op;
    }

    public InvokerOperation<T> CreateOperation<T>()
    {
        return Connection.Pools.GetInvokerOperation<T>();
    }

    public VoidInvokerOperation CreateOperation()
    {
        return Connection.Pools.GetInvokerOperation();
    }

    public Task WaitForFreeOperationSlot(CancellationToken cancellationToken)
    {
        return semaphore.WaitAsync(cancellationToken);
    }

    public void RegisterOperation(InvokerOperation operation)
    {
        using (_lock.EnterScope()) // spinlock most likely would be wasteful here. Lock.Enter does spin-waiting when needed
        {
            _lastOpId++;
            while (Array.IndexOf(_operationIds, _lastOpId, 0, _allocated) >= 0)
            {
                _lastOpId++;
            }

            int freeSlot = Array.IndexOf(_operations, null);
            Debug.Assert(freeSlot >= 0);
            operation.Token = _lastOpId;
            _operations[freeSlot] = operation;
            _allocated++;
        }
    }
}
