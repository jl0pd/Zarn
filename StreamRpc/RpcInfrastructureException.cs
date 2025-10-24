namespace StreamRpc;

internal sealed class RpcInfrastructureException : RpcException
{
    public RpcInfrastructureException()
    {
    }

    public RpcInfrastructureException(string? message) : base(message)
    {
    }

    public RpcInfrastructureException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    public RpcInfrastructureException(Exception? innerException)
        : base("An exception has been thrown by RPC infrastructure, see InnerException for more details", innerException)
    {
    }
}
