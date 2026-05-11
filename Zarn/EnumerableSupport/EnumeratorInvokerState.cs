using System.Diagnostics;
using Zarn.Invocation;
using Zarn.Protocol;
using Zarn.Protocol.Messages;

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

    protected override async void BeginAcquireRemoteIdCore()
    {
        Connection.Dispatch(new GetEnumeratorMessageRequest
        {
            InvokerId = Id,
            EnumerableId = _enumerableId,
            IsAsync = _isAsync,
            TypeArg = _typeArg,
        });
    }
}
