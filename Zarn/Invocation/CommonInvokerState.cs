using System.Diagnostics;
using Zarn.Protocol;

namespace Zarn.Invocation;

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
