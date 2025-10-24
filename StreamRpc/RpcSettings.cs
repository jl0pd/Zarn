using StreamRpc.Serialization;

namespace StreamRpc;

public sealed class RpcSettings
{
    /// <summary>
    /// List of serializers for user-provided types.
    /// Serializer must implement <see cref="BinarySerializer{T}"/> or <see cref="BinarySerializerFactory"/>.
    /// </summary>
    /// <remarks>
    /// Type may be attributed with <see cref="BinarySerializerAttribute"/> or <see cref="BinarySerializerAttribute{T}"/>,
    /// this way it may not be passed here. If type has both attribute and was registered here, this list takes precedence.
    /// </remarks>
    public IList<BinarySerializer> Serializers { get; } = new List<BinarySerializer>();

    public MemoryProvider? MemoryProvider { get; set; }

    /// <summary>
    /// List of exceptions that are propagated to caller from remote without wrapping.
    /// Exception type must exactly match.
    /// </summary>
    public IList<Type> TransparentExceptions { get; } = BinarySerializationContext.ExceptionSerializers.Keys.ToList();

    /// <summary>
    /// Indicates whether communication is allowed when protocol version has changed in insignificant way.
    /// Minor protocol revision changes when new message type is added. So client and server may still communicate
    /// if they are sure that newer features are not used. Major protocol revision changes when existing message is changed.
    /// Client and server cannot communicate when major version is different.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool AllowMinorVersionMismatch { get; set; } = true;

    /// <summary>
    /// Controls behavior when exception has occurred and must be passed to remote caller.
    /// Defaults to <see cref="UnhandledExceptionPropagationBehavior.TransparentNoWrap"/>
    /// </summary>
    public UnhandledExceptionPropagationBehavior UnhandledExceptionPropagationBehavior { get; set; }
        = UnhandledExceptionPropagationBehavior.TransparentNoWrap;
}
