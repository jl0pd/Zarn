using System.Buffers;
using StreamRpc.Collections;

namespace StreamRpc.Serialization.Serializers.Core.Collections;

internal sealed class SmallArray0BinarySerializerFactory : BinarySerializerFactory
{
    public static SmallArray0BinarySerializerFactory Instance { get; } = new();

    public override bool CanConvert(Type type)
    {
        return type.IsConstructedGenericType
            && type.GetGenericTypeDefinition() is { } typeDef
            && typeDef == typeof(SmallArray0<>);
    }

    public override BinarySerializer CreateSerializer(Type type)
    {
        var arg = type.GetGenericArguments()[0];
        var serType = typeof(SmallArray0BinarySerializer<>).MakeGenericType(arg);
        return (BinarySerializer)Activator.CreateInstance(serType)!;
    }

    private sealed class SmallArray0BinarySerializer<T> : BinarySerializer<SmallArray0<T>>
    {
        internal override byte[] TypePrefix => _typePrefix ??= GetTypePrefix(typeof(T[]));

        public override SmallArray0<T> Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
        {
            int length = context.Deserialize<int>(ref source);
            if (length != 0)
            {
                throw new InvalidDataException($"Expected array of length 0, got {length}");
            }

            return SmallArray.Create<T>();
        }

        public override void Serialize(SmallArray0<T> value, IBufferWriter<byte> writer, BinarySerializationContext context)
        {
            context.Serialize(0, writer);
        }
    }
}
