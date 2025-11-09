using System.ComponentModel;
using System.Runtime.CompilerServices;
using StreamRpc.Compression;
using StreamRpc.Serialization;
using StreamRpc.Utils;

namespace StreamRpc;

public sealed class RpcSettings : ICloneable
{
    private readonly StrongBox<bool> _isFrozen = new(false);

    /// <summary>
    /// List of serializers for user-provided types.
    /// Serializer must implement <see cref="BinarySerializer{T}"/> or <see cref="BinarySerializerFactory"/>.
    /// </summary>
    /// <remarks>
    /// Type may implement <see cref="IBinarySerializable{TSelf}"/> or be attributed
    /// with <see cref="BinarySerializerAttribute"/> or <see cref="BinarySerializerAttribute{T}"/>,
    /// this way serializer doesn't have to be passed here. This list takes precedence over other methods.
    /// </remarks>
    public IList<BinarySerializer> Serializers { get; }

    /// <summary>
    /// List of <see cref="CompressionProvider"/>s that are used during communication if both parties support same algorithm.
    /// Providers at start of list has higher priority.
    /// </summary>
    /// <remarks>
    /// List may be cleared to disable compression, useful in scenarios when underlying stream already does compression.
    /// Compression is not enabled by default. <see cref="BrotliCompressionProvider"/> or custom implementation
    /// can be added if needed.
    /// </remarks>
    public IList<CompressionProvider> CompressionProviders { get; }

    /// <summary>
    /// List of exceptions that are propagated to caller from remote without wrapping.
    /// Exception type must exactly match.
    /// </summary>
    public IList<Type> TransparentExceptions { get; }

    /// <summary>
    /// Indicates whether communication is allowed when protocol version has changed in insignificant way.
    /// Minor protocol revision changes when new message type is added. So client and server may still communicate
    /// if they are sure that newer features are not used. Major protocol revision changes when existing message is changed.
    /// Client and server cannot communicate when major version is different.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool AllowMinorVersionMismatch
    {
        get => _allowMinorVersionMismatch;
        set
        {
            ThrowIfFrozen();
            _allowMinorVersionMismatch = value;
        }
    }
    private bool _allowMinorVersionMismatch = true;

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
            ThrowIfFrozen();
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
            ThrowIfFrozen();
            if (value is <= UnhandledExceptionPropagationBehavior.Hidden or > UnhandledExceptionPropagationBehavior.TransparentNoWrap)
            {
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(UnhandledExceptionPropagationBehavior));
            }
            _unhandledExceptionPropagationBehavior = value;
        }
    }
    private UnhandledExceptionPropagationBehavior _unhandledExceptionPropagationBehavior = UnhandledExceptionPropagationBehavior.TransparentNoWrap;

    public RpcSettings()
    {
        Serializers = new FreezableList<BinarySerializer>(_isFrozen);
        TransparentExceptions = new FreezableList<Type>(_isFrozen, BinarySerializationContext.ExceptionSerializers.Keys);
        CompressionProviders = new FreezableList<CompressionProvider>(_isFrozen);
    }

    /// <summary>
    /// Freezes current instance so it cannot be modified.
    /// Instance is automatically frozen when it's passed into <see cref="RpcServer(RpcStreamProvider, RpcSettings?)"/>
    /// or <see cref="RpcClient(RpcStreamProvider, RpcSettings?)"/>.
    /// </summary>
    public void Freeze()
    {
        _isFrozen.Value = true;
    }

    /// <summary>
    /// Returns new unfrozen copy of this instance.
    /// </summary>
    public RpcSettings Clone()
    {
        var result = new RpcSettings
        {
            _allowMinorVersionMismatch = _allowMinorVersionMismatch,
            _maxConcurrentOperations = _maxConcurrentOperations,
            _unhandledExceptionPropagationBehavior = _unhandledExceptionPropagationBehavior
        };

        foreach (var item in Serializers)
        {
            result.Serializers.Add(item);
        }

        foreach (var item in TransparentExceptions)
        {
            result.TransparentExceptions.Add(item);
        }

        return result;
    }

    object ICloneable.Clone() => Clone();

    private void ThrowIfFrozen()
    {
        if (_isFrozen.Value)
        {
            throw new InvalidOperationException("Current instance is frozen and cannot be modified");
        }
    }
}
