using System.Buffers;
using System.Diagnostics;
using Zarn.Compression;
using Zarn.Serialization;

namespace Zarn.Protocol;

internal struct ExecuteRequestMessage
{
    public ExecuteRequestOptions Options;
    public OperationId OperationId;
    public ObjectId RemoteId;
    public int MethodSlot;

    public const int MaxHeaderSize = sizeof(MessageType)
                                   + sizeof(ExecuteRequestOptions)
                                   + ObjectId.MaxSize
                                   + OperationId.MaxSize
                                   + sizeof(int);

    private readonly int CompressedHeaderSize => sizeof(MessageType)
                                               + sizeof(ExecuteRequestOptions)
                                               + OperationId.CompressedSize
                                               + RemoteId.CompressedSize
                                               + PackedInt.GetRequiredSize(MethodSlot);

    public SequenceReader<byte> Deserialize(ChunkedArrayPoolBufferWriter<byte> message,
                                            BinarySerializationContext context,
                                            Pools pools,
                                            out ChunkedArrayPoolBufferWriter<byte>? uncompressed)
    {
        var reader = message.GetReader();
        DeserializeHeader(ref reader, context);

        if (!Options.HasFlag(ExecuteRequestOptions.Compressed))
        {
            uncompressed = null;
            return reader;
        }
        else
        {
            var decompressor = pools.TryGetDecompressor() ?? throw ThrowHelper.Unreachable;
            uncompressed = pools.GetWriter();
            decompressor.Decompress(reader.UnreadSequence, uncompressed);
            pools.Return(decompressor);

            return uncompressed.GetReader();
        }
    }

    private void DeserializeHeader(ref SequenceReader<byte> reader, BinarySerializationContext context)
    {
        var type = context.Deserialize<MessageType>(ref reader);
        Debug.Assert(type == MessageType.ExecuteRequest);

        Options = context.Deserialize<ExecuteRequestOptions>(ref reader);
        OperationId = context.Deserialize<OperationId>(ref reader);
        RemoteId = context.Deserialize<ObjectId>(ref reader);
        MethodSlot = context.Deserialize<int>(ref reader);
    }

    public readonly void ReplacePlaceholders(ChunkedArrayPoolBufferWriter<byte> writer)
    {
        var chunk = writer.FirstChunkRequired;

        var headerSize = CompressedHeaderSize;

        var span = chunk.Array.AsSpan(chunk.Start - headerSize, headerSize);
        span[0] = (byte)MessageType.ExecuteRequest;
        span[1] = (byte)Options;
        int advance = OperationId.Serialize(span[2..]) + 2;
        advance += RemoteId.Serialize(span[advance..]);
        PackedInt.Write(MethodSlot, span[advance..]);

        chunk.Written += headerSize;
        chunk.Start -= headerSize;
        Debug.Assert(chunk.Start >= 0);
    }

    public readonly void Compress(ChunkedArrayPoolBufferWriter<byte> writer,
                                  ICompressor compressor,
                                  ChunkedArrayPoolBufferWriter<byte> destination)
    {
        var chunk = writer.FirstChunkRequired;

        var headerSize = CompressedHeaderSize;

        destination.Reserve(PackedInt.MaxSize);
        destination.Write(chunk.WrittenSpan[..headerSize]);

        var source = writer.GetSequence().Slice(headerSize);
        compressor.Compress(source, destination);
    }
}
