using System.Buffers;

namespace Zarn.Serialization.Serializers.Optional;

internal sealed class ListBinarySerializerFactory : BinarySerializerFactory
{
    public static ListBinarySerializerFactory Instance { get; } = new();

    public override bool CanConvert(Type type)
        => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>);

    public override BinarySerializer CreateSerializer(Type type)
    {
        var elementType = type.GetGenericArguments()[0];
        var serializerType = typeof(Serializer<>).MakeGenericType(elementType);
        return (BinarySerializer)Activator.CreateInstance(serializerType)!;
    }

    private sealed class Serializer<T> : BinarySerializer<List<T>?>
    {
        public override List<T>? Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
        {
            int count = context.Deserialize<int>(ref source);
            if (count == -1)
            {
                return null;
            }
            else if (count < 0)
            {
                throw new InvalidDataException();
            }
            else
            {
                var list = new List<T>(count);
                for (int i = 0; i < count; i++)
                {
                    list.Add(context.Deserialize<T>(ref source));
                }
                return list;
            }
        }

        public override void Serialize(List<T>? value, IBufferWriter<byte> writer, BinarySerializationContext context)
        {
            if (value is null)
            {
                context.Serialize(-1, writer);
            }
            else
            {
                context.Serialize(value.Count, writer);
                foreach (var item in value)
                {
                    context.Serialize(item, writer);
                }
            }
        }
    }
}
