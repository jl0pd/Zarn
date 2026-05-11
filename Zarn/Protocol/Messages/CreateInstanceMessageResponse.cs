using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using Zarn.Serialization;

namespace Zarn.Protocol.Messages;

internal struct CreateInstanceMessageResponse : IMessageInternal<CreateInstanceMessageResponse>
{
    public ObjectId InvokerId { get; set; }

    [MemberNotNullWhen(false, nameof(Exception))]
    public bool IsSuccess { get; set; }

    public Exception? Exception { get; set; }

    public ObjectId ObjectId { get; set; }

    public readonly MessageType Type => MessageType.CreateInstanceResponse;

    public static CreateInstanceMessageResponse Deserialize(ref SequenceReader<byte> reader, BinarySerializationContext context)
    {
        var invokerId = context.Deserialize<ObjectId>(ref reader);
        var isSuccess = context.Deserialize<bool>(ref reader);
        if (isSuccess)
        {
            return new CreateInstanceMessageResponse
            {
                InvokerId = invokerId,
                IsSuccess = true,
                ObjectId = context.Deserialize<ObjectId>(ref reader),
            };
        }
        else
        {
            return new CreateInstanceMessageResponse
            {
                InvokerId = invokerId,
                IsSuccess = false,
                Exception = (Exception?)context.DeserializeAny(ref reader),
            };
        }
    }

    public readonly void Serialize(IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(InvokerId, writer);
        if (IsSuccess)
        {
            context.Serialize(true, writer);
            context.Serialize(ObjectId, writer);
        }
        else
        {
            context.Serialize(false, writer);
            context.SerializeAny(Exception, writer);
        }
    }
}
