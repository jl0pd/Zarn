using System.Buffers;
using System.Diagnostics;
using Zarn.Serialization;

namespace Zarn.Protocol;

internal struct ExecuteResponseMessage
{
    public ExecuteResponseOptions Options { get; set; }

    public OperationId OperationId { get; set; }

    public SequenceReader<byte> Deserialize(ChunkedArrayPoolBufferWriter<byte> message,
                                            BinarySerializationContext context,
                                            Pools pools,
                                            out ChunkedArrayPoolBufferWriter<byte>? uncompressed)
    {
        var reader = message.GetReader();
        DeserializeHeader(ref reader, context);

        if (Options.HasFlag(ExecuteResponseOptions.Compressed))
        {
            uncompressed = pools.GetWriter();
            var decompressor = pools.TryGetDecompressor() ?? throw ThrowHelper.Unreachable;

            decompressor.Decompress(reader.UnreadSequence, uncompressed);

            pools.Return(decompressor);
            return uncompressed.GetReader();
        }
        else
        {
            uncompressed = null;
            return reader;
        }
    }

    private void DeserializeHeader(ref SequenceReader<byte> reader, BinarySerializationContext context)
    {
        var type = context.Deserialize<MessageType>(ref reader);
        Debug.Assert(type == MessageType.ExecuteResponse);

        Options = context.Deserialize<ExecuteResponseOptions>(ref reader);
        OperationId = context.Deserialize<OperationId>(ref reader);
    }
}
