using System.Buffers;
using System.Diagnostics;

namespace Zarn.Protocol;

internal sealed class InvokerState
{
    public required ObjectId Id { get; init; }

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
    private readonly ObjectId _enumerableId;
    private readonly bool _enumerableIsAsync;

    public InvokerState(ConnectionContext context, int typeSlot, Type[] genericArgs)
    {
        _semaphore = context.ConcurrentOperationsSemaphore;
        Connection = context;
        _typeSlot = typeSlot;
        _genericArgs = genericArgs;
        _operationIds = new short[context.MaxConcurrentOperations];
        _operations = new InvokerOperation[context.MaxConcurrentOperations];
        RemoteId = _remoteIdTcs.Task;
    }

    public InvokerState(ConnectionContext context, ObjectId enumerableId, bool enumerableIsAsync, Type genericArg)
    {
        _semaphore = context.ConcurrentOperationsSemaphore;
        Connection = context;
        _typeSlot = -1;
        _genericArgs = [genericArg];
        _operationIds = new short[context.MaxConcurrentOperations];
        _operations = new InvokerOperation[context.MaxConcurrentOperations];
        RemoteId = _remoteIdTcs.Task;
        _enumerableId = enumerableId;
        _enumerableIsAsync = enumerableIsAsync;
    }

    public InvokerState(ConnectionContext context, ObjectId remoteId)
    {
        _semaphore = context.ConcurrentOperationsSemaphore;
        Connection = context;
        _typeSlot = -1;
        _genericArgs = [];
        _operationIds = new short[context.MaxConcurrentOperations];
        _operations = new InvokerOperation[context.MaxConcurrentOperations];
        RemoteId = Task.FromResult(remoteId);
        _remoteIdTcs = null;
        _remoteIdAcquiring = 1;
    }

    public void SetRemoteId(ObjectId id)
    {
        Debug.Assert(_remoteIdTcs is { });
        _remoteIdTcs.SetResult(id);
        _remoteIdTcs = null;
        _remoteIdAcquiring = 1; // prevent `BeginAcquireRemoteId` from running when it's already acquired.
    }

    public void BeginAcquireRemoteId()
    {
        if (Interlocked.Exchange(ref _remoteIdAcquiring, 1) == 0)
        {
            AcquireRemoteIdCore();
        }
    }

    private async void AcquireRemoteIdCore()
    {
        Debug.Assert(_remoteIdTcs is { });
        try
        {
            ObjectId remoteId;
            if (_typeSlot == -1)
            {
                if (_enumerableIsAsync)
                {
                    remoteId = await Connection.RemoteInstanceManager.GetAsyncEnumerator(_enumerableId, _genericArgs[0]);
                }
                else
                {
                    remoteId = await Connection.RemoteInstanceManager.GetEnumerator(_enumerableId, _genericArgs[0]);
                }
            }
            else
            {
                remoteId = await Connection.RemoteInstanceManager.CreateInstance(_typeSlot, _genericArgs);
            }

            _remoteIdTcs.SetResult(remoteId);
        }
        catch (Exception e)
        {
            _remoteIdTcs.SetException(e);
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
