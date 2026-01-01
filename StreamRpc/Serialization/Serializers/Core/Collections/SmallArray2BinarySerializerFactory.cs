using System.Buffers;
using StreamRpc.Collections;

namespace StreamRpc.Serialization.Serializers.Core.Collections;

internal sealed class SmallArray2BinarySerializerFactory : BinarySerializerFactory
{
    public static SmallArray2BinarySerializerFactory Instance { get; } = new();

    public override bool CanConvert(Type type)
    {
        return type.IsConstructedGenericType
            && type.GetGenericTypeDefinition() is { } typeDef
            && typeDef == typeof(SmallArray2<>);
    }

    public override BinarySerializer CreateSerializer(Type type)
    {
        var arg = type.GetGenericArguments()[0];
        var serType = typeof(SmallArray2BinarySerializer<>).MakeGenericType(arg);
        return (BinarySerializer)Activator.CreateInstance(serType)!;
    }

    private sealed class SmallArray2BinarySerializer<T> : BinarySerializer<SmallArray2<T>>
    {
        internal override byte[] TypePrefix => _typePrefix ??= GetTypePrefix(typeof(T[]));

        public override SmallArray2<T> Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
        {
            int length = context.Deserialize<int>(ref source);
            if (length != 2)
            {
                throw new InvalidDataException($"Expected array of length 2, got {length}");
            }

            return SmallArray.Create(context.Deserialize<T>(ref source), context.Deserialize<T>(ref source));
        }

        public override void Serialize(SmallArray2<T> value, IBufferWriter<byte> writer, BinarySerializationContext context)
        {
            context.Serialize(2, writer);
            context.Serialize(value.First, writer);
            context.Serialize(value.Second, writer);
        }
    }
}
