using System.Buffers;
using Zarn.Collections;

namespace Zarn.Serialization.Serializers.Core.Collections;

internal sealed class SmallArray1BinarySerializerFactory : BinarySerializerFactory
{
    public static SmallArray1BinarySerializerFactory Instance { get; } = new();

    public override bool CanConvert(Type type)
    {
        return type.IsConstructedGenericType
            && type.GetGenericTypeDefinition() is { } typeDef
            && typeDef == typeof(SmallArray1<>);
    }

    public override BinarySerializer CreateSerializer(Type type)
    {
        var arg = type.GetGenericArguments()[0];
        var serType = typeof(SmallArray1BinarySerializer<>).MakeGenericType(arg);
        return (BinarySerializer)Activator.CreateInstance(serType)!;
    }

    private sealed class SmallArray1BinarySerializer<T> : BinarySerializer<SmallArray1<T>>
    {
        internal override byte[] TypePrefix => _typePrefix ??= GetTypePrefix(typeof(T[]));

        public override SmallArray1<T> Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
        {
            int length = context.Deserialize<int>(ref source);
            if (length != 1)
            {
                throw new InvalidDataException($"Expected array of length 1, got {length}");
            }

            return SmallArray.Create(context.Deserialize<T>(ref source));
        }

        public override void Serialize(SmallArray1<T> value, IBufferWriter<byte> writer, BinarySerializationContext context)
        {
            context.Serialize(1, writer);
            context.Serialize(value.First, writer);
        }
    }
}
