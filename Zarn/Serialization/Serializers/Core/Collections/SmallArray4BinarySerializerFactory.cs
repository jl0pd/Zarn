using System.Buffers;
using Zarn.Collections;

namespace Zarn.Serialization.Serializers.Core.Collections;

internal sealed class SmallArray4BinarySerializerFactory : BinarySerializerFactory
{
    public static SmallArray4BinarySerializerFactory Instance { get; } = new();

    public override bool CanConvert(Type type)
    {
        return type.IsConstructedGenericType
            && type.GetGenericTypeDefinition() is { } typeDef
            && typeDef == typeof(SmallArray4<>);
    }

    public override BinarySerializer CreateSerializer(Type type)
    {
        var arg = type.GetGenericArguments()[0];
        var serType = typeof(SmallArray4BinarySerializer<>).MakeGenericType(arg);
        return (BinarySerializer)Activator.CreateInstance(serType)!;
    }

    private sealed class SmallArray4BinarySerializer<T> : BinarySerializer<SmallArray4<T>>
    {
        internal override byte[] TypePrefix => _typePrefix ??= GetTypePrefix(typeof(T[]));

        public override SmallArray4<T> Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
        {
            int length = context.Deserialize<int>(ref source);
            if (length != 4)
            {
                throw new InvalidDataException($"Expected array of length 4, got {length}");
            }

            return SmallArray.Create(context.Deserialize<T>(ref source),
                                     context.Deserialize<T>(ref source),
                                     context.Deserialize<T>(ref source),
                                     context.Deserialize<T>(ref source));
        }

        public override void Serialize(SmallArray4<T> value, IBufferWriter<byte> writer, BinarySerializationContext context)
        {
            context.Serialize(4, writer);
            context.Serialize(value.First, writer);
            context.Serialize(value.Second, writer);
            context.Serialize(value.Third, writer);
            context.Serialize(value.Fourth, writer);
        }
    }
}
