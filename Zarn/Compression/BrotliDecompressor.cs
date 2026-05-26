using System.Buffers;
using System.IO.Compression;

namespace Zarn.Compression;

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
            OperationStatus status;
            int written;
            do
            {
                var dstSpan = destination.GetSpan(source.Length);
                status = decoder.Decompress(source, dstSpan, out int consumed, out written);
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
            } while (status == OperationStatus.DestinationTooSmall && written > 0);
        }
    }

    public void Dispose()
    {
    }
}
