using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Core;

internal sealed class BinarySerializableFactory : BinarySerializerFactory
{
    public static BinarySerializableFactory Instance { get; } = new();

    public override bool CanConvert(Type type)
    {
        var interfaces = type.GetInterfaces();
        foreach (var i in interfaces)
        {
            if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IBinarySerializable<>))
            {
                return true;
            }
        }

        return false;
    }

    public override BinarySerializer CreateSerializer(Type type)
    {
        var interfaces = type.GetInterfaces();
        foreach (var i in interfaces)
        {
            if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IBinarySerializable<>))
            {
                var getArgType = i.GetGenericArguments()[0];
                var serType = typeof(Serializer<>).MakeGenericType(getArgType);
                return (BinarySerializer)Activator.CreateInstance(serType)!;
            }
        }

        throw ThrowHelper.Unreachable;
    }

    private sealed class Serializer<T> : BinarySerializer<T> where T : IBinarySerializable<T>
    {
        public override T Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
            => T.Deserialize(ref source, context);

        public override void Serialize(T value, IBufferWriter<byte> writer, BinarySerializationContext context)
            => value.Serialize(writer, context);
    }
}
