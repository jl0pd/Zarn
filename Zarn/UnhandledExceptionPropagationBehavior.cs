namespace Zarn;

/// <summary>
/// Controls behavior when exception has occurred and must be passed to remote caller.
/// </summary>
public enum UnhandledExceptionPropagationBehavior
{
    /// <summary>
    /// Exception is ignored, caller will see <see cref="UnhandledRpcException"/> with message indicating internal error.
    /// </summary>
    /// <remarks>
    /// Used when caller is not trusted and implementation detail of server should be hidden.
    /// </remarks>
    Hidden,

    /// <summary>
    /// Call <see cref="Exception.ToString"/> and pass it as <see cref="Exception.Message"/> to <see cref="UnhandledRpcException"/>.
    /// </summary>
    WrapToString,

    /// <summary>
    /// Create <see cref="UnhandledRpcException"/> with actual exception
    /// transparently passed inside <see cref="Exception.InnerException"/>.
    /// If exception cannot be passed transparently,
    /// then <see cref="Exception.ToString"/> is called and it's passed as message to thrown <see cref="UnhandledRpcException"/>
    /// </summary>
    /// <remarks>
    /// Used when it's desired to catch any exception occurred through RPC using <c>catch(UnhandledRpcException)</c>
    /// </remarks>
    TransparentWrap,

    /// <summary>
    /// Passes exception transparently to caller.
    /// If exception cannot be passed transparently,
    /// then <see cref="Exception.ToString"/> is called and it's passed as message to thrown <see cref="UnhandledRpcException"/>
    /// </summary>
    /// <remarks>
    /// Used when the fact that call was made using RPC should be hidden.
    /// </remarks>
    TransparentNoWrap,
}
