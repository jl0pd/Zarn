using System.Buffers;
using System.Diagnostics;

namespace Zarn.Protocol;

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

    public void SetRemoteId(ObjectId id)
    {
        _remoteIdAcquiring = 1; // prevent `BeginAcquireRemoteId` from running when it's already acquired.
        SetRemoteIdCore(id);
    }

    protected abstract void SetRemoteIdCore(ObjectId id);

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

internal sealed class CommonInvokerState : InvokerState
{
    private readonly int _typeSlot;
    private readonly Type[] _genericArgs;
    private TaskCompletionSource<ObjectId>? _remoteIdTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override Task<ObjectId> RemoteId { get; }

    public CommonInvokerState(ConnectionContext connection, int typeSlot, Type[] genericArgs) : base(connection)
    {
        _typeSlot = typeSlot;
        _genericArgs = genericArgs;
        RemoteId = _remoteIdTcs.Task;
    }

    protected override void SetRemoteIdCore(ObjectId id)
    {
        Debug.Assert(_remoteIdTcs is { });
        _remoteIdTcs.SetResult(id);
        _remoteIdTcs = null;
    }

    protected override async void BeginAcquireRemoteIdCore()
    {
        Debug.Assert(_remoteIdTcs is { });
        try
        {
            var rid = await Connection.RemoteInstanceManager.CreateInstance(_typeSlot, _genericArgs);
            _remoteIdTcs.SetResult(rid);
        }
        catch (Exception e)
        {
            _remoteIdTcs.SetException(e);
        }
        _remoteIdTcs = null;
    }
}

internal sealed class ExistingInvokerState(ConnectionContext connection, ObjectId remoteId) : InvokerState(connection)
{
    public override Task<ObjectId> RemoteId { get; } = Task.FromResult(remoteId);

    protected override void BeginAcquireRemoteIdCore()
    {
        // RemoteId is already set in constructor
        throw ThrowHelper.Unreachable;
    }

    protected override void SetRemoteIdCore(ObjectId id)
    {
        // RemoteId is already set in constructor
        throw ThrowHelper.Unreachable;
    }
}

internal sealed class EnumeratorInvokerState : InvokerState
{
    private readonly ObjectId _enumerableId;
    private readonly bool _isAsync;
    private readonly Type _typeArg;
    private TaskCompletionSource<ObjectId>? _remoteIdTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override Task<ObjectId> RemoteId { get; }

    public EnumeratorInvokerState(ConnectionContext connection,
                                  ObjectId enumerableId,
                                  bool isAsync,
                                  Type typeArg) : base(connection)
    {
        _enumerableId = enumerableId;
        _isAsync = isAsync;
        _typeArg = typeArg;
        RemoteId = _remoteIdTcs.Task;
    }

    protected override void SetRemoteIdCore(ObjectId id)
    {
        Debug.Assert(_remoteIdTcs is { });
        _remoteIdTcs.SetResult(id);
        _remoteIdTcs = null;
    }

    protected override async void BeginAcquireRemoteIdCore()
    {
        Debug.Assert(_remoteIdTcs is { });
        try
        {
            var rid = await (_isAsync
                                ? Connection.RemoteInstanceManager.GetAsyncEnumerator(_enumerableId, _typeArg)
                                : Connection.RemoteInstanceManager.GetEnumerator(_enumerableId, _typeArg));

            _remoteIdTcs.SetResult(rid);
        }
        catch (Exception e)
        {
            _remoteIdTcs.SetException(e);
        }
        _remoteIdTcs = null;
    }
}