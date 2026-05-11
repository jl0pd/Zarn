using System.Buffers;
using Zarn.Protocol;
using Zarn.Protocol.Messages;
using Zarn.Serialization;

namespace Zarn.EnumerableSupport;

internal struct GetEnumeratorMessageRequest : IMessageInternal<GetEnumeratorMessageRequest>
{
    public ObjectId InvokerId { get; set; }

    public bool IsAsync { get; set; }

    public ObjectId EnumerableId { get; set; }

    public Type TypeArg { get; set; }

    public readonly MessageType Type => MessageType.GetEnumeratorRequest;

    public static GetEnumeratorMessageRequest Deserialize(ref SequenceReader<byte> reader, BinarySerializationContext context)
    {
        return new GetEnumeratorMessageRequest
        {
            InvokerId = context.Deserialize<ObjectId>(ref reader),
            IsAsync = context.Deserialize<bool>(ref reader),
            EnumerableId = context.Deserialize<ObjectId>(ref reader),
            TypeArg = context.Deserialize<Type>(ref reader),
        };
    }

    public readonly void Serialize(IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(InvokerId, writer);
        context.Serialize(IsAsync, writer);
        context.Serialize(EnumerableId, writer);
        context.Serialize(TypeArg, writer);
    }
}
