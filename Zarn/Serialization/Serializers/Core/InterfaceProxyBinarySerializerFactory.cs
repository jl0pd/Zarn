using System.Buffers;
using System.Runtime.CompilerServices;
using Zarn.Protocol;
using Zarn.Protocol.EnumerableSupport;
using Zarn.TypeGeneration;

namespace Zarn.Serialization.Serializers.Core;

internal sealed class InterfaceProxyBinarySerializerFactory(StrongBox<ConnectionContext?> connection) : BinarySerializerFactory
{
    public override bool CanConvert(Type type)
    {
        // BinarySerializationContext is public and can be used outside of RPC,
        // so don't catch interfaces when it may be unexpected from user
        return connection.Value is { } && type.IsInterface;
    }

    public override BinarySerializer CreateSerializer(Type type)
    {
        var actualConnection = connection.Value ?? throw ThrowHelper.Unreachable;
        var strategyType = DetermineStrategy(type);
        var serType = typeof(Serializer<,>).MakeGenericType(type, strategyType);
        var serializer = (BinarySerializer)Activator.CreateInstance(serType, [actualConnection])!;
        return serializer;
    }

    private static Type DetermineStrategy(Type type)
    {
        if (type.IsConstructedGenericType)
        {
            var typeDef = type.GetGenericTypeDefinition();
            if (typeDef == typeof(IEnumerable<>) || typeDef == typeof(IAsyncEnumerable<>))
            {
                return typeof(EnumerableHandlingStrategy<>).MakeGenericType(type.GetGenericArguments());
            }
        }

        return typeof(AnyHandlingStrategy<>).MakeGenericType(type);
    }

    private enum ValueKind : byte
    {
        Null,
        Invoker,
        Any,
    }

    private interface IAnyHandlingStrategy
    {
        static abstract ObjectId Register(ConnectionContext connection, object value);

        static abstract object ResolveInstance(ConnectionContext connection, ObjectId id);
    }

    private sealed class AnyHandlingStrategy<T> : IAnyHandlingStrategy
    {
        public static ObjectId Register(ConnectionContext connection, object value)
        {
            return connection.InstanceManager.Register(value, connection.InstanceManager.GetCalleeFactory(typeof(T)));
        }

        public static object ResolveInstance(ConnectionContext connection, ObjectId id)
        {
            var invoker = connection.InstanceManager.GetInvoker(typeof(T), connection.GenObjectId(), true);
            invoker.State.SetRemoteId(id);
            return invoker;
        }
    }

    private sealed class EnumerableHandlingStrategy<T> : IAnyHandlingStrategy
    {
        public static ObjectId Register(ConnectionContext connection, object value)
        {
            return connection.InstanceManager.Register(value, UnreachableCalleeFactory.Instance);
        }

        public static object ResolveInstance(ConnectionContext connection, ObjectId id)
        {
            var invoker = new EnumerableInvoker<T>
            {
                State = new ExistingInvokerState(connection, id)
                {
                    Id = connection.GenObjectId()
                },
            };
            return invoker;
        }
    }

    private sealed class Serializer<T, TStrategy>(ConnectionContext connection) : BinarySerializer<T?>
        where T : class
        where TStrategy : IAnyHandlingStrategy
    {
        public override T? Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
        {
            var kind = context.Deserialize<ValueKind>(ref source);
            switch (kind)
            {
                case ValueKind.Null:
                    return null;
                case ValueKind.Invoker:
                    var id = context.Deserialize<ObjectId>(ref source);
                    var invoker = connection.InstanceManager.GetDescriptor(id).Instance;
                    return (T)invoker;
                case ValueKind.Any:
                    id = context.Deserialize<ObjectId>(ref source);
                    var anyInvoker = TStrategy.ResolveInstance(connection, id);
                    return (T)anyInvoker;
                default:
                    throw new InvalidDataException();
            }
        }

        public override void Serialize(T? value, IBufferWriter<byte> writer, BinarySerializationContext context)
        {
            switch (value)
            {
                case null:
                    context.Serialize(ValueKind.Null, writer);
                    break;
                case InvokerBase invoker when invoker.State.Connection == connection:
                    context.Serialize(ValueKind.Invoker, writer);
                    context.Serialize(invoker.State.RemoteId.GetAwaiter().GetResult(), writer);
                    break;
                default:
                    context.Serialize(ValueKind.Any, writer);
                    var id = TStrategy.Register(connection, value);
                    context.Serialize(id, writer);
                    break;
            }
        }
    }
}
