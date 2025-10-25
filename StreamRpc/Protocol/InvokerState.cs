using System.Buffers;
using System.Diagnostics;
using StreamRpc.Serialization;

namespace StreamRpc.Protocol;

internal sealed class InvokerState
{
    public ObjectId Id { get; } = ObjectId.GenObjectId();

    public Task<ObjectId> RemoteId { get; }

    public ConnectionContext Connection { get; }

    private int _allocated = 0;
    private readonly short[] _operationIds;
    private readonly InvokerOperation?[] _operations;
    private short _lastOpId;
    private readonly Lock _lock = new();
    private readonly int _typeSlot;
    private readonly Type[] _genericArgs;
    private readonly SemaphoreSlim _semaphore;
    private TaskCompletionSource<ObjectId>? _remoteIdTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _remoteIdAcquiring = 0;

    public InvokerState(ConnectionContext context,
                        int typeSlot,
                        Type[] genericArgs,
                        int maxConcurrentOperations,
                        SemaphoreSlim semaphore)
    {
        _semaphore = semaphore;
        Connection = context;
        _typeSlot = typeSlot;
        _genericArgs = genericArgs;
        _operationIds = new short[maxConcurrentOperations];
        _operations = new InvokerOperation[maxConcurrentOperations];
        RemoteId = _remoteIdTcs.Task;
    }

    public void SetRemoteId(ObjectId id)
    {
        Debug.Assert(_remoteIdTcs is { });
        _remoteIdTcs.SetResult(id);
        _remoteIdTcs = null;
    }

    public void BeginAcquireRemoteId()
    {
        if (Interlocked.Exchange(ref _remoteIdAcquiring, 1) == 0)
        {
            var writer = Connection.Pools.GetWriter();
            writer.Reserve(PackedInt.MaxSize);
            Connection.SerializationContext.Serialize(MessageType.GetRemoteIdRequest, writer);
            Connection.SerializationContext.Serialize(Id, writer);
            Connection.SerializationContext.Serialize(_typeSlot, writer);
            Connection.SerializationContext.Serialize(_genericArgs, writer);
            Connection.Dispatch(writer, null);
        }
    }

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
        _semaphore.Release();
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
        return _semaphore.WaitAsync(cancellationToken);
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
