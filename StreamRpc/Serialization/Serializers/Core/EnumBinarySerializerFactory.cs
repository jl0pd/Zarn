using System.Buffers;
using System.Runtime.CompilerServices;

namespace StreamRpc.Serialization.Serializers.Core;

internal sealed class EnumBinarySerializerFactory : BinarySerializerFactory
{
    public static EnumBinarySerializerFactory Instance { get; } = new();

    public override bool CanConvert(Type type) => type.IsEnum;

    public override BinarySerializer CreateSerializer(Type type)
    {
        var underlyingType = type.GetEnumUnderlyingType();

        var serType = Type.GetTypeCode(underlyingType) switch
        {
            TypeCode.SByte or
            TypeCode.Byte or
            TypeCode.Boolean => typeof(byte),

            TypeCode.Char or
            TypeCode.Int16 or
            TypeCode.UInt16 => typeof(short),

            TypeCode.Int32 or
            TypeCode.UInt32 or
            TypeCode.Single => typeof(int),

            TypeCode.Int64 or
            TypeCode.UInt64 or
            TypeCode.Double => typeof(long),

            _ => throw new NotSupportedException("Unsupported underlying enum type: " + underlyingType)
        };

        return (BinarySerializer)Activator.CreateInstance(typeof(Serializer<,>).MakeGenericType([serType, type]))!;
    }

    private sealed class Serializer<TStorage, TValue> : BinarySerializer<TValue>
    where TValue : Enum
    where TStorage : struct
    {
        public override TValue Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
        {
            var result = context.Deserialize<TStorage>(ref source);
            return Unsafe.As<TStorage, TValue>(ref result);
        }

        public override void Serialize(TValue value, IBufferWriter<byte> writer, BinarySerializationContext context)
        {
            context.Serialize(Unsafe.As<TValue, TStorage>(ref value), writer);
        }
    }
}
