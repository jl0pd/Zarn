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
}
