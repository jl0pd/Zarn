namespace Zarn;

/// <summary>
/// Base type for exceptions raised by this library
/// </summary>
public abstract class RpcException : Exception
{
    protected RpcException()
    {
    }

    protected RpcException(string? message) : base(message)
    {
    }

    protected RpcException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
