using System.ComponentModel;
using StreamRpc.Compression;
using StreamRpc.Serialization;

namespace StreamRpc;

// TODO: freeze instance
public sealed class RpcSettings
{
    /// <summary>
    /// List of serializers for user-provided types.
    /// Serializer must implement <see cref="BinarySerializer{T}"/> or <see cref="BinarySerializerFactory"/>.
    /// </summary>
    /// <remarks>
    /// Type may implement <see cref="IBinarySerializable{TSelf}"/> or be attributed
    /// with <see cref="BinarySerializerAttribute"/> or <see cref="BinarySerializerAttribute{T}"/>,
    /// this way serializer doesn't have to be passed here. This list takes precedence over other methods.
    /// </remarks>
    public IList<BinarySerializer> Serializers { get; } = new List<BinarySerializer>();

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
    /// Limits maximum concurrent operations that can be made.
    /// If limit is reached, then new operation is put into queue.
    /// Value must be in range [1; 65536]. Defaults to 100.
    /// </summary>
    /// <remarks>
    /// Value that is too low can lead to deadlocks if high amount of recursive calls is made.
    /// </remarks>
    public int MaxConcurrentOperations
    {
        get => _maxConcurrentOperations;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 65536);
            _maxConcurrentOperations = value;
        }
    }
    private int _maxConcurrentOperations = 100;

    /// <summary>
    /// Controls behavior when exception has occurred and must be passed to remote caller.
    /// Defaults to <see cref="UnhandledExceptionPropagationBehavior.TransparentNoWrap"/>
    /// </summary>
    public UnhandledExceptionPropagationBehavior UnhandledExceptionPropagationBehavior
    {
        get => _unhandledExceptionPropagationBehavior;
        set
        {
            if (value is <= UnhandledExceptionPropagationBehavior.Hidden or > UnhandledExceptionPropagationBehavior.TransparentNoWrap)
            {
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(UnhandledExceptionPropagationBehavior));
            }
            _unhandledExceptionPropagationBehavior = value;
        }
    }
    private UnhandledExceptionPropagationBehavior _unhandledExceptionPropagationBehavior = UnhandledExceptionPropagationBehavior.TransparentNoWrap;
}
