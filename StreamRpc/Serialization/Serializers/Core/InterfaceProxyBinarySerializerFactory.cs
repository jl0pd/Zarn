using System.Buffers;
using System.Runtime.CompilerServices;
using StreamRpc.Protocol;

namespace StreamRpc.Serialization.Serializers.Core;

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
        var serializer = (BinarySerializer)Activator.CreateInstance(typeof(Serializer<>).MakeGenericType(type), [actualConnection])!;
        return serializer;
    }

    private enum ValueKind : byte
    {
        Null,
        Invoker,
        Any,
    }

    private sealed class Serializer<T>(ConnectionContext connection) : BinarySerializer<T?> where T : class
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
                    var invoker = connection.GetInstance(id);
                    return (T)invoker;
                case ValueKind.Any:
                    id = context.Deserialize<ObjectId>(ref source);
                    var anyInvoker = connection.GetInvoker(typeof(T), true);
                    anyInvoker.State.SetRemoteId(id);
                    return (T)(object)anyInvoker;
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
                    var factory = connection.GetCalleeFactory(typeof(T));
                    var id = connection.RegisterInstance(value, factory);
                    context.Serialize(id, writer);
                    break;
            }
        }
    }
}
