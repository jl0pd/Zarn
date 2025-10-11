namespace StreamRpc;

public class ClientConnectedEventArgs(RpcClient client) : EventArgs
{
    public RpcClient Client { get; } = client;
}
