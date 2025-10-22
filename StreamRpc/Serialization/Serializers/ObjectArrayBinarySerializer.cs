using System.Buffers;

namespace StreamRpc.Serialization.Serializers;

internal sealed class ObjectArrayBinarySerializer : BinarySerializer<object?[]?>
{
    public static ObjectArrayBinarySerializer Instance { get; } = new();

    public override bool CanConvert(Type type) => type == typeof(object[]);

    public override object?[]? Deserialize(ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        var length = context.Deserialize<int>(ref source);
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

                var result = new object?[length];
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = context.DeserializeAny(ref source);
                }

                return result;
        }
    }

    public override void Serialize(object?[]? value, IBufferWriter<byte> writer, BinarySerializationContext context)
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
                context.SerializeAny(item, writer);
            }
        }
    }
}
