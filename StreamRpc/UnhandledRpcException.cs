namespace StreamRpc;

/// <summary>
/// Carries the exception that was thrown in remote
/// </summary>
public class UnhandledRpcException : RpcException
{
    public UnhandledRpcException()
    {
    }

    public UnhandledRpcException(string? message) : base(message)
    {
    }

    public UnhandledRpcException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
