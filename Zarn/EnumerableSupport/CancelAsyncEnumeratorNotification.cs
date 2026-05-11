using System.Buffers;
using Zarn.Protocol;
using Zarn.Protocol.Messages;
using Zarn.Serialization;

namespace Zarn.EnumerableSupport;

internal struct CancelAsyncEnumeratorNotification : IMessageInternal<CancelAsyncEnumeratorNotification>
{
    public ObjectId EnumeratorId { get; set; }

    public readonly MessageType Type => MessageType.CancelAsyncEnumeratorNotification;

    public static CancelAsyncEnumeratorNotification Deserialize(ref SequenceReader<byte> reader, BinarySerializationContext context)
    {
        return new CancelAsyncEnumeratorNotification
        {
            EnumeratorId = context.Deserialize<ObjectId>(ref reader),
        };
    }

    public readonly void Serialize(IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(EnumeratorId, writer);
    }
}
