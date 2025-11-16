using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using StreamRpc.Compression;
using StreamRpc.Serialization;

namespace StreamRpc.Protocol;

internal struct ExecuteRequestMessage
{
    public ExecuteRequestOptions Options;
    public OperationId OperationId;
    public ObjectId RemoteId;
    public int MethodSlot;
    public Type[]? GenericMethodArgs;

    private const int NonCompressedLength = sizeof(MessageType)
                                          + sizeof(ExecuteRequestOptions)
                                          + OperationId.Size
                                          + ObjectId.Size;

    public long Deserialize(ChunkedArrayPoolBufferWriter<byte> message,
                            BinarySerializationContext context,
                            Pools pools,
                            out ChunkedArrayPoolBufferWriter<byte>? uncompressed)
    {
        var reader = message.GetReader();
        DeserializeHeader(ref reader, context);

        if (!Options.HasFlag(ExecuteRequestOptions.Compressed))
        {
            DeserializeRest(ref reader, context);
            uncompressed = null;
            return reader.Consumed;
        }
        else
        {
            var decompressor = pools.GetDecompressor() ?? throw ThrowHelper.Unreachable;
            uncompressed = pools.GetWriter();
            decompressor.Decompress(reader.Sequence.Slice(reader.Consumed), uncompressed);
            pools.Return(decompressor);

            var uncompressedReader = uncompressed.GetReader();
            DeserializeRest(ref uncompressedReader, context);

            return uncompressedReader.Consumed;
        }
    }

    private void DeserializeHeader(ref SequenceReader<byte> reader, BinarySerializationContext context)
    {
        var type = context.Deserialize<MessageType>(ref reader);
        Debug.Assert(type == MessageType.ExecuteRequest);

        Options = context.Deserialize<ExecuteRequestOptions>(ref reader);
        OperationId = context.Deserialize<OperationId>(ref reader);
        RemoteId = context.Deserialize<ObjectId>(ref reader);
    }

    private void DeserializeRest(ref SequenceReader<byte> reader, BinarySerializationContext context)
    {
        MethodSlot = context.Deserialize<int>(ref reader);
        GenericMethodArgs = Options.HasFlag(ExecuteRequestOptions.GenericMethod)
                            ? context.Deserialize<Type[]>(ref reader)
                            : Type.EmptyTypes;
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

    public static void Compress(ChunkedArrayPoolBufferWriter<byte> writer, ICompressor compressor, IBufferWriter<byte> destination)
    {
        var ar = writer.FirstChunkRequired.Array;

        var nonCompressed = destination.GetSpan(PackedInt.MaxSize + NonCompressedLength);
        ar.AsSpan(0, PackedInt.MaxSize + NonCompressedLength).CopyTo(nonCompressed);
        destination.Advance(PackedInt.MaxSize + NonCompressedLength);

        var source = writer.GetSequence().Slice(PackedInt.MaxSize + NonCompressedLength);
        compressor.Compress(source, destination);
    }
}
