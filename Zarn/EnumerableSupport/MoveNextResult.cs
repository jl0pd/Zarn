using System.Buffers;
using Zarn.Serialization;

namespace Zarn.EnumerableSupport;

internal readonly record struct MoveNextResult<T>(bool Success, T? Current) : IBinarySerializable<MoveNextResult<T>>
{
    public static MoveNextResult<T> Deserialize(ref SequenceReader<byte> reader, BinarySerializationContext context)
    {
        var success = context.Deserialize<bool>(ref reader);
        if (success)
        {
            var current = context.Deserialize<T>(ref reader);
            return new MoveNextResult<T>(true, current);
        }
        return default;
    }

    public void Serialize(IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        if (Success)
        {
            context.Serialize(true, writer);
            context.Serialize(Current, writer);
        }
        else
        {
            context.Serialize(false, writer);
        }
    }
}
