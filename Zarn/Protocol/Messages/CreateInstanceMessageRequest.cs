using System.Buffers;
using Zarn.Serialization;

namespace Zarn.Protocol.Messages;

internal struct CreateInstanceMessageRequest : IMessageInternal<CreateInstanceMessageRequest>
{
    public ObjectId InvokerId { get; set; }

    public int TypeSlot { get; set; }

    public Type[] GenericArgs { get; set; }

    public readonly MessageType Type => MessageType.CreateInstanceRequest;

    public static CreateInstanceMessageRequest Deserialize(ref SequenceReader<byte> reader, BinarySerializationContext context)
    {
        return new CreateInstanceMessageRequest
        {
            InvokerId = context.Deserialize<ObjectId>(ref reader),
            TypeSlot = context.Deserialize<int>(ref reader),
            GenericArgs = context.Deserialize<Type[]>(ref reader),
        };
    }

    public readonly void Serialize(IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(InvokerId, writer);
        context.Serialize(TypeSlot, writer);
        context.Serialize(GenericArgs, writer);
    }
}
