using Zarn.Protocol;

namespace Zarn.Invocation;

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
