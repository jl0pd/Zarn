using System.Buffers;
using Zarn.Collections;

namespace Zarn.Serialization.Serializers.Core.Collections;

internal sealed class SmallArray3BinarySerializerFactory : BinarySerializerFactory
{
    public static SmallArray3BinarySerializerFactory Instance { get; } = new();

    public override bool CanConvert(Type type)
    {
        return type.IsConstructedGenericType
            && type.GetGenericTypeDefinition() is { } typeDef
            && typeDef == typeof(SmallArray3<>);
    }

    public override BinarySerializer CreateSerializer(Type type)
    {
        var arg = type.GetGenericArguments()[0];
        var serType = typeof(SmallArray3BinarySerializer<>).MakeGenericType(arg);
        return (BinarySerializer)Activator.CreateInstance(serType)!;
    }

    private sealed class SmallArray3BinarySerializer<T> : BinarySerializer<SmallArray3<T>>
    {
        internal override byte[] TypePrefix => _typePrefix ??= GetTypePrefix(typeof(T[]));

        public override SmallArray3<T> Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
        {
            int length = context.Deserialize<int>(ref source);
            if (length != 3)
            {
                throw new InvalidDataException($"Expected array of length 3, got {length}");
            }

            return SmallArray.Create(context.Deserialize<T>(ref source),
                                     context.Deserialize<T>(ref source),
                                     context.Deserialize<T>(ref source));
        }

        public override void Serialize(SmallArray3<T> value, IBufferWriter<byte> writer, BinarySerializationContext context)
        {
            context.Serialize(3, writer);
            context.Serialize(value.First, writer);
            context.Serialize(value.Second, writer);
            context.Serialize(value.Third, writer);
        }
    }
}
