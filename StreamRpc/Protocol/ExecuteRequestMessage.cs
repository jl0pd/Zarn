using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using StreamRpc.Serialization;

namespace StreamRpc.Protocol;

internal struct ExecuteRequestMessage
{
    public ExecuteRequestOptions Options;
    public OperationId OperationId;
    public ObjectId RemoteId;
    public int MethodSlot;
    public Type[]? GenericMethodArgs;
    public long ReaderOffset;

    public void Deserialize(ChunkedArrayPoolBufferWriter<byte> message, BinarySerializationContext context)
    {
        var reader = message.GetReader();
        Deserialize(ref reader, context);
    }

    public void Deserialize(ref SequenceReader<byte> reader, BinarySerializationContext context)
    {
        var type = context.Deserialize<MessageType>(ref reader);
        Debug.Assert(type == MessageType.ExecuteRequest);

        Options = context.Deserialize<ExecuteRequestOptions>(ref reader);
        OperationId = context.Deserialize<OperationId>(ref reader);
        RemoteId = context.Deserialize<ObjectId>(ref reader);
        MethodSlot = context.Deserialize<int>(ref reader);
        GenericMethodArgs = Options.HasFlag(ExecuteRequestOptions.GenericMethod)
                            ? context.Deserialize<Type[]>(ref reader)
                            : Type.EmptyTypes;
        ReaderOffset = reader.Consumed;
    }

    public readonly void Serialize(IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(MessageType.ExecuteRequest, writer);
        context.Serialize(Options, writer);
        context.Serialize(OperationId, writer);
        context.Serialize(RemoteId, writer);
        context.Serialize(MethodSlot, writer);
        if (Options.HasFlag(ExecuteRequestOptions.GenericMethod))
        {
            Debug.Assert(GenericMethodArgs is { Length: > 0 });
            context.Serialize(GenericMethodArgs, writer);
        }
    }

    public static void ReplacePlaceholders(ChunkedArrayPoolBufferWriter<byte> writer,
                                           ObjectId invokerId,
                                           short operationToken,
                                           ObjectId remoteId)
    {
        var ar = writer.FirstChunkRequired.Array;

        var targetSpan = ar.AsSpan(PackedInt.MaxSize + sizeof(MessageType) + sizeof(ExecuteRequestOptions));
        MemoryMarshal.Write(targetSpan, new OperationId(invokerId, operationToken));
        targetSpan = targetSpan[OperationId.Size..];

        MemoryMarshal.Write(targetSpan, remoteId);
    }
}
