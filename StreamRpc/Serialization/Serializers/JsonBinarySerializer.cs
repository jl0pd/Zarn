using System.Buffers;
using System.Diagnostics;
using System.Text.Json;

namespace StreamRpc.Serialization.Serializers;

internal sealed class JsonBinarySerializer<T>(JsonSerializerOptions options) : BinarySerializer<T?>
{
    public override T? Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        var json = context.Deserialize<string?>(ref source);
        if (json is null)
        {
            Debug.Assert(!typeof(T).IsValueType, "Structs shall not be serialized as null");
            return default;
        }
        else
        {
            var value = JsonSerializer.Deserialize<T>(json, options);
            return value;
        }
    }

    public override void Serialize(T? value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        if (value is null)
        {
            context.Serialize<string?>(null, writer);
        }
        else
        {
            string json = JsonSerializer.Serialize(value, options);
            context.Serialize(json, writer);
        }
    }
}
