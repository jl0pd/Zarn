using System.Buffers;
using Zarn.Serialization;

namespace Zarn.Protocol.Messages;

internal struct ExecuteCancelNotification : IMessageInternal<ExecuteCancelNotification>
{
    public OperationId OperationId { get; set; }

    public readonly MessageType Type => MessageType.ExecuteCancelNotification;

    public static ExecuteCancelNotification Deserialize(ref SequenceReader<byte> reader, BinarySerializationContext context)
    {
        return new ExecuteCancelNotification
        {
            OperationId = context.Deserialize<OperationId>(ref reader),
        };
    }

    public readonly void Serialize(IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(OperationId, writer);
    }
}
