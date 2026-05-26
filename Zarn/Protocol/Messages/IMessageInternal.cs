using System.Diagnostics;
using Zarn.Collections;
using Zarn.Serialization;

namespace Zarn.Protocol.Messages;

internal interface IMessageInternal<T> : IBinarySerializable<T> where T : struct, IBinarySerializable<T>
{
    MessageType Type { get; }

    public static T Deserialize(ChunkedArrayPoolBufferWriter<byte> buffer, BinarySerializationContext context)
    {
        var reader = buffer.GetReader();
        var result = T.Deserialize(ref reader, context);
        Debug.Assert(reader.End);
        return result;
    }
}
