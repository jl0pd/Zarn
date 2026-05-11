using System.Buffers;
using Zarn.Serialization;

namespace Zarn.Protocol.Messages;

internal struct ObjectCollectedNotification : IMessageInternal<ObjectCollectedNotification>
{
    public ObjectId InstanceId { get; set; }

    public readonly MessageType Type => MessageType.ObjectCollectedNotification;

    public static ObjectCollectedNotification Deserialize(ref SequenceReader<byte> reader, BinarySerializationContext context)
    {
        return new ObjectCollectedNotification
        {
            InstanceId = context.Deserialize<ObjectId>(ref reader),
        };
    }

    public readonly void Serialize(IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(InstanceId, writer);
    }
}
