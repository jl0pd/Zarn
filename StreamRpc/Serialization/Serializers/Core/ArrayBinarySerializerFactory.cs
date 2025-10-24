using System.Buffers;

namespace StreamRpc.Serialization.Serializers.Core;

internal sealed class ArrayBinarySerializerFactory : BinarySerializerFactory
{
    public static ArrayBinarySerializerFactory Instance { get; } = new();

    public override bool CanConvert(Type type)
    {
        return type.IsSZArray;
    }

    public override BinarySerializer CreateSerializer(Type type)
    {
        return (BinarySerializer)Activator.CreateInstance(typeof(Serializer<>).MakeGenericType(type.GetElementType()!))!;
    }

    private sealed class Serializer<T> : BinarySerializer<T[]?>
    {
        public override T[]? Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
        {
            int length = context.Deserialize<int>(ref source);
            switch (length)
            {
                case -1:
                    return null;
                case 0:
                    return [];
                default:
                    if (length < 0)
                    {
                        throw new InvalidDataException();
                    }

                    var result = new T[length];
                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = context.Deserialize<T>(ref source);
                    }

                    return result;
            }
        }

        public override void Serialize(T[]? value, IBufferWriter<byte> writer, BinarySerializationContext context)
        {
            if (value is null)
            {
                context.Serialize(-1, writer);
            }
            else
            {
                context.Serialize(value.Length, writer);
                foreach (var item in value)
                {
                    context.Serialize(item, writer);
                }
            }
        }
    }
}
