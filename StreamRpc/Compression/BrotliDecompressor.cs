using System.Buffers;
using System.IO.Compression;

namespace StreamRpc.Compression;

// source is adapted from System.IO.Compression.BrotliStream
public sealed class BrotliDecompressor : IDecompressor
{
    public void Decompress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination)
    {
        if (source.IsEmpty)
        {
            return;
        }

        // cannot pass `using var` by reference
        var decoder = new BrotliDecoder();
        try
        {
            DecompressCore(ref decoder, source, destination);
        }
        finally
        {
            decoder.Dispose();
        }
    }

    private void DecompressCore(ref BrotliDecoder decoder,
                                ReadOnlySequence<byte> source,
                                IBufferWriter<byte> destination)
    {
        foreach (var chunk in source)
        {
            DecompressChunk(ref decoder, chunk.Span, destination);
        }
    }

    private static void DecompressChunk(ref BrotliDecoder decoder,
                                        ReadOnlySpan<byte> source,
                                        IBufferWriter<byte> destination)
    {
        while (!source.IsEmpty)
        {
            var dstSpan = destination.GetSpan(source.Length);
            var status = decoder.Decompress(source, dstSpan, out int consumed, out int written);
            if (status == OperationStatus.InvalidData)
            {
                ThrowHelper.ThrowInvalidData();
            }

            if (consumed > 0)
            {
                source = source[consumed..];
            }

            if (written > 0)
            {
                destination.Advance(written);
            }
        }
    }

    public void Dispose()
    {
    }
}
