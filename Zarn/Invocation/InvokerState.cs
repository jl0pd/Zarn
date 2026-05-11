using System.Buffers;
using System.Diagnostics;
using Zarn.Protocol;
using Zarn.Protocol.Messages;

namespace Zarn.Invocation;

internal abstract class InvokerState(ConnectionContext connection)
{
    public required ObjectId Id { get; init; }

    public abstract Task<ObjectId> RemoteId { get; }

    public ConnectionContext Connection { get; } = connection;

    private int _allocated = 0;
    private readonly short[] _operationIds = new short[connection.MaxConcurrentOperations];
    private readonly InvokerOperation?[] _operations = new InvokerOperation[connection.MaxConcurrentOperations];
    private short _lastOpId;
    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _semaphore = connection.ConcurrentOperationsSemaphore;
    private int _remoteIdAcquiring = 0;

    public void SetRemoteId(ref readonly CreateInstanceMessageResponse message)
    {
        _remoteIdAcquiring = 1; // prevent `BeginAcquireRemoteId` from running when it's already acquired.
        SetRemoteIdCore(in message);
    }

    protected abstract void SetRemoteIdCore(ref readonly CreateInstanceMessageResponse message);

    public void BeginAcquireRemoteId()
    {
        if (Interlocked.Exchange(ref _remoteIdAcquiring, 1) == 0)
        {
            BeginAcquireRemoteIdCore();
        }
    }

    protected abstract void BeginAcquireRemoteIdCore();

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
        lock (_lock)
        {
            // cannot use `Array.IndexOf(_operationIds, opId)` because `operationIds` may contain same id,
            // but corresponding operation will be null.

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
        _semaphore.Release();
        return op;
    }

    public Task WaitForFreeOperationSlot(CancellationToken cancellationToken)
    {
        return _semaphore.WaitAsync(cancellationToken);
    }

    public void RegisterOperation(InvokerOperation operation)
    {
        lock (_lock) // spinlock most likely would be wasteful here. Lock.Enter does spin-waiting when needed
        {
            _lastOpId++;
            while (Array.IndexOf(_operationIds, _lastOpId) >= 0)
            {
                _lastOpId++;
            }

            int freeSlot = Array.IndexOf(_operations, null);
            Debug.Assert(freeSlot >= 0);
            operation.Token = _lastOpId;
            _operations[freeSlot] = operation;
            _operationIds[freeSlot] = _lastOpId;
            _allocated++;
        }
    }

    public void OnCollected()
    {
        //Debug.Fail("not implemented");
    }
}
