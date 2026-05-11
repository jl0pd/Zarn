using Zarn.Protocol;
using Zarn.Protocol.Messages;

namespace Zarn.Invocation;

internal sealed class ExistingInvokerState(ConnectionContext connection, ObjectId remoteId) : InvokerState(connection)
{
    public override Task<ObjectId> RemoteId { get; } = Task.FromResult(remoteId);

    protected override void BeginAcquireRemoteIdCore()
    {
        // RemoteId is already set in constructor
        throw ThrowHelper.Unreachable;
    }

    protected override void SetRemoteIdCore(ref readonly CreateInstanceMessageResponse response)
    {
        // RemoteId is already set in constructor
        throw ThrowHelper.Unreachable;
    }
}
