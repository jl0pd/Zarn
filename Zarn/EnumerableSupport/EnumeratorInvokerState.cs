using System.Diagnostics;
using Zarn.Invocation;
using Zarn.Protocol;

namespace Zarn.EnumerableSupport;

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