using System.Diagnostics;
using Zarn.Protocol;
using Zarn.Protocol.Messages;

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

    protected override void SetRemoteIdCore(ref readonly CreateInstanceMessageResponse message)
    {
        Debug.Assert(_remoteIdTcs is { });
        if (message.IsSuccess)
        {
            _remoteIdTcs.SetResult(message.ObjectId);
        }
        else
        {
            _remoteIdTcs.SetException(message.Exception);
        }

        _remoteIdTcs = null;
    }

    protected override void BeginAcquireRemoteIdCore()
    {
        Connection.Dispatch(new CreateInstanceMessageRequest
        {
            InvokerId = Id,
            TypeSlot = _typeSlot,
            GenericArgs = _genericArgs,
        });
    }
}
