using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using StreamRpc.Protocol;
using StreamRpc.Serialization.Serializers;
using StreamRpc.Serialization.Serializers.Core;
using StreamRpc.Serialization.Serializers.Exceptions;

namespace StreamRpc.Serialization;

public sealed class BinarySerializationContext
{
    private readonly Dictionary<Type, BinarySerializer> _instances = new()
    {
        { typeof(Type), TypeBinarySerializer.Instance },
        { typeof(byte), ByteBinarySerializer.Instance },
        { typeof(short), PackedShortBinarySerializer.Instance },
        { typeof(int), PackedIntBinarySerializer.Instance },
        { typeof(long), PackedLongBinarySerializer.Instance },
        { typeof(bool), BoolBinarySerializer.Instance },
        { typeof(string), StringBinarySerializer.Instance },
        { typeof(byte[]), ByteArrayBinarySerializer.Instance },
        { typeof(object[]), ObjectArrayBinarySerializer.Instance },
        { typeof(CancellationToken), CancellationTokenBinarySerializer.Instance },
        { typeof(ReadOnlyMemory<byte>), ByteReadOnlyMemoryBinarySerializer.Instance },
    };

    private readonly List<BinarySerializerFactory> _factories =
    [
        EnumBinarySerializerFactory.Instance,
        UnmanagedBinarySerializerFactory.Instance,
        ArrayBinarySerializerFactory.Instance,
        BinarySerializableFactory.Instance,
    ];

    internal static Dictionary<Type, BinarySerializer> ExceptionSerializers { get; } = new()
    {
        { typeof(Exception), ExceptionBinarySerializer.Instance },
        { typeof(InvalidOperationException), InvalidOperationExceptionBinarySerializer.Instance },
        { typeof(DivideByZeroException), DivideByZeroExceptionBinarySerializer.Instance },
        { typeof(HttpIOException), HttpIOExceptionBinarySerializer.Instance },
        { typeof(HttpProtocolException), HttpProtocolExceptionBinarySerializer.Instance },
        { typeof(DriveNotFoundException), DriveNotFoundExceptionBinarySerializer.Instance },
        { typeof(KeyNotFoundException), KeyNotFoundExceptionBinarySerializer.Instance },
        { typeof(StackOverflowException), StackOverflowExceptionBinarySerializer.Instance },
        { typeof(AccessViolationException), AccessViolationExceptionBinarySerializer.Instance },
        { typeof(ArithmeticException), ArithmeticExceptionBinarySerializer.Instance },
        { typeof(DirectoryNotFoundException), DirectoryNotFoundExceptionBinarySerializer.Instance },
        { typeof(ApplicationException), ApplicationExceptionBinarySerializer.Instance },
        { typeof(NotFiniteNumberException), NotFiniteNumberExceptionBinarySerializer.Instance },
        { typeof(TaskCanceledException), TaskCanceledExceptionBinarySerializer.Instance },
        { typeof(FormatException), FormatExceptionBinarySerializer.Instance },
        { typeof(UriFormatException), UriFormatExceptionBinarySerializer.Instance },
        { typeof(TimeoutException), TimeoutExceptionBinarySerializer.Instance },
        { typeof(FileNotFoundException), FileNotFoundExceptionBinarySerializer.Instance },
        { typeof(PlatformNotSupportedException), PlatformNotSupportedExceptionBinarySerializer.Instance },
        { typeof(NotSupportedException), NotSupportedExceptionBinarySerializer.Instance },
        { typeof(IndexOutOfRangeException), IndexOutOfRangeExceptionBinarySerializer.Instance },
        { typeof(EndOfStreamException), EndOfStreamExceptionBinarySerializer.Instance },
        { typeof(DataMisalignedException), DataMisalignedExceptionBinarySerializer.Instance },
        { typeof(InvalidCastException), InvalidCastExceptionBinarySerializer.Instance },
        { typeof(NotImplementedException), NotImplementedExceptionBinarySerializer.Instance },
        { typeof(OverflowException), OverflowExceptionBinarySerializer.Instance },
        { typeof(PathTooLongException), PathTooLongExceptionBinarySerializer.Instance },
        { typeof(OperationCanceledException), OperationCanceledExceptionBinarySerializer.Instance },
        { typeof(ObjectDisposedException), ObjectDisposedExceptionBinarySerializer.Instance },
        { typeof(InvalidDataException), InvalidDataExceptionBinarySerializer.Instance },
        { typeof(UnreachableException), UnreachableExceptionBinarySerializer.Instance },
        { typeof(OutOfMemoryException), OutOfMemoryExceptionBinarySerializer.Instance },
        { typeof(IOException), IOExceptionBinarySerializer.Instance },
        { typeof(RankException), RankExceptionBinarySerializer.Instance },
        { typeof(SemaphoreFullException), SemaphoreFullExceptionBinarySerializer.Instance },
        { typeof(SystemException), SystemExceptionBinarySerializer.Instance },
        { typeof(ArrayTypeMismatchException), ArrayTypeMismatchExceptionBinarySerializer.Instance },
        { typeof(NullReferenceException), NullReferenceExceptionBinarySerializer.Instance },
        { typeof(ArgumentException), ArgumentExceptionBinarySerializer.Instance },
        { typeof(ArgumentNullException), ArgumentNullExceptionBinarySerializer.Instance },
        { typeof(ArgumentOutOfRangeException), ArgumentOutOfRangeExceptionBinarySerializer.Instance },
        { typeof(AggregateException), AggregateExceptionBinarySerializer.Instance },
    };

    private readonly JsonBinarySerializerFactory _jsonFactory = new();
    private readonly StrongBox<ConnectionContext?> _connection = new();

    private readonly MemoryProvider? _memoryProvider;

    public BinarySerializationContext(RpcSettings settings)
    {
        _memoryProvider = settings.MemoryProvider;

        foreach (var serializer in settings.Serializers)
        {
            if (serializer is BinarySerializerFactory factory)
            {
                _factories.Add(factory);
            }
            else
            {
                var serializerType = serializer.GetType();
                Debug.Assert(serializerType.IsConstructedGenericType &&
                             serializerType.GetGenericTypeDefinition() == typeof(BinarySerializer<>),
                             "Only factory & generic serializers are implementable");

                var valueType = serializerType.GetGenericArguments()[0];
                _instances.Add(valueType, serializer);
            }
        }

        foreach (var exType in settings.TransparentExceptions)
        {
            if (ExceptionSerializers.TryGetValue(exType, out var serializer))
            {
                Debug.Assert(serializer is not BinarySerializerFactory);
                _instances.TryAdd(exType, serializer);
            }
        }

        _factories.Add(new InterfaceProxyBinarySerializerFactory(_connection));
    }

    internal void SetConnection(ConnectionContext connection)
    {
        Debug.Assert(_connection.Value is null);
        _connection.Value = connection;
    }

    public BinarySerializer GetSerializer(Type type)
    {
        if (_instances.TryGetValue(type, out var serializer))
        {
            return serializer;
        }
        return GetSerializerSlow(type);
    }

    private BinarySerializer GetSerializerSlow(Type type)
    {
        if ((GetSerializerFromFactory(type) ??
             GetSerializerFromInstance(type) ??
             GetSerializerFromAttribute(type)) is { } ser)
        {
            return ser;
        }
        ;
        return _instances[type] = _jsonFactory.CreateSerializer(type);
    }

    private BinarySerializer? GetSerializerFromAttribute(Type type)
    {
        if (type.GetCustomAttribute<BinarySerializerAttribute>() is { } binSerAttr)
        {
            var serInst = Activator.CreateInstance(binSerAttr.SerializerType)!;
            if (binSerAttr.SerializerType.IsAssignableTo(typeof(BinarySerializerFactory)))
            {
                var factory = (BinarySerializerFactory)serInst;
                _factories.Add(factory);
                return factory.CreateSerializer(type);
            }
            else
            {
                var ser2 = (BinarySerializer)serInst;
                _instances[type] = ser2;
                return ser2;
            }
        }

        return null;
    }

    private BinarySerializer? GetSerializerFromFactory(Type type)
    {
        foreach (var factory in _factories)
        {
            if (factory.CanConvert(type))
            {
                var serializer = factory.CreateSerializer(type);
                _instances[type] = serializer;
                return serializer;
            }
        }

        return null;
    }

    private BinarySerializer? GetSerializerFromInstance(Type type)
    {
        foreach (var (_, inst) in _instances)
        {
            if (inst.CanConvert(type))
            {
                _instances[type] = inst;
                return inst;
            }
        }

        return null;
    }

    public BinarySerializer<T> GetSerializer<T>()
    {
        return (BinarySerializer<T>)GetSerializer(typeof(T));
    }

    public void SerializeAny(object? value, IBufferWriter<byte> writer)
    {
        if (value is null)
        {
            writer.GetSpan()[0] = (byte)ObjectType.Null;
            writer.Advance(1);
        }
        else
        {
            var serializer = GetSerializer(value.GetType());
            writer.Write(serializer.TypePrefix);
            serializer.Serialize(value, value.GetType(), writer, this);
        }
    }

    public object? DeserializeAny(ref SequenceReader<byte> source)
    {
        var objType = (ObjectType)source.UnreadSpan[0];
        source.Advance(1);
        if (objType == ObjectType.Null)
        {
            return null;
        }
        else
        {
            object? result = objType switch
            {
                ObjectType.String => GetSerializer<string>().Deserialize(ref source, this),
                ObjectType.Int => GetSerializer<int>().Deserialize(ref source, this),
                ObjectType.Type => GetSerializer<Type>().Deserialize(ref source, this),
                ObjectType.Byte | ObjectType.Array => GetSerializer<byte[]>().Deserialize(ref source, this),
                ObjectType.Byte => GetSerializer<byte>().Deserialize(ref source, this),
                ObjectType.Method => GetSerializer<MethodInfo>().Deserialize(ref source, this),
                ObjectType.Custom => ReadCustomObject(ref source),
                _ => throw new InvalidDataException(),
            };
            return result;
        }
    }

    private object? ReadCustomObject(ref SequenceReader<byte> source)
    {
        var type = GetSerializer<Type>().Deserialize(ref source, this);
        var binarySerializer = GetSerializer(type);
        var result = binarySerializer.Deserialize(type, ref source, this);
        return result;
    }

    [SuppressMessage("Usage", "CA2263:Prefer generic overload when type is known")]
    public void Serialize<T>(T value, IBufferWriter<byte> writer)
    {
        var serializer = GetSerializer(typeof(T));
        if (serializer is BinarySerializer<T> ser)
        {
            ser.Serialize(value, writer, this);
        }
        else
        {
            serializer.Serialize(value, typeof(T), writer, this);
        }
    }

    [SuppressMessage("Usage", "CA2263:Prefer generic overload when type is known")]
    public T Deserialize<T>(ref SequenceReader<byte> source)
    {
        var serializer = GetSerializer(typeof(T));
        if (serializer is BinarySerializer<T> ser)
        {
            return ser.Deserialize(ref source, this);
        }
        else
        {
            return (T)serializer.Deserialize(typeof(T), ref source, this)!;
        }
    }

    public IMemoryOwner<byte> ToMemory(ReadOnlySequence<byte> source)
        => _memoryProvider?.ToMemory(source) ?? new ArrayMemoryOwner<byte>(source.ToArray());

    public IMemoryOwner<byte> ToMemory(ReadOnlySpan<byte> source)
        => _memoryProvider?.ToMemory(source) ?? new ArrayMemoryOwner<byte>(source.ToArray());
}
