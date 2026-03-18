using System.Buffers;
using System.ComponentModel;
using System.IO.Compression;

namespace Zarn.Compression;

// source is adapted from System.IO.Compression.BrotliStream
/// <summary>
/// </summary>
/// <param name="quality">A number representing quality of the Brotli compression. 
/// 0 is the minimum (no compression), 11 is the maximum.</param>
/// <param name="window">A number representing the encoder window bits.
/// The minimum value is 10, and the maximum value is 24</param>
public sealed class BrotliCompressor(int quality, int window) : ICompressor
{
    // 22 is taken from BrotliEncoder.TryCompress
    public BrotliCompressor(CompressionLevel level) : this(GetQuality(level), 22)
    {
    }

    private static int GetQuality(CompressionLevel level) => level switch
    {
        CompressionLevel.Optimal => 4,
        CompressionLevel.Fastest => 1,
        CompressionLevel.NoCompression => 0,
        CompressionLevel.SmallestSize => 11,
        _ => throw new InvalidEnumArgumentException(nameof(level),
                                                    (int)level,
                                                    typeof(CompressionLevel)),
    };

    public void Compress(ReadOnlySequence<byte> source, IBufferWriter<byte> destination)
    {
        if (source.IsEmpty)
        {
            return;
        }

        // cannot pass `using var` by reference
        var encoder = new BrotliEncoder(quality, window);
        var buffer = ArrayPool<byte>.Shared.Rent(8192);
        try
        {
            foreach (var chunk in source)
            {
                WriteCore(ref encoder, buffer, destination, chunk.Span, false);
            }

            WriteCore(ref encoder, buffer, destination, [], true);
        }
        finally
        {
            encoder.Dispose();
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void WriteCore(ref BrotliEncoder encoder,
                           byte[] buffer,
                           IBufferWriter<byte> writer,
                           ReadOnlySpan<byte> source,
                           bool isFinalBlock)
    {
        var operationStatus = OperationStatus.DestinationTooSmall;
        var destination = new Span<byte>(buffer);
        while (operationStatus == OperationStatus.DestinationTooSmall)
        {
            operationStatus = encoder.Compress(source, destination, out var bytesConsumed, out var bytesWritten, isFinalBlock);
            if (operationStatus == OperationStatus.InvalidData)
            {
                throw new InvalidDataException();
            }
            if (bytesWritten > 0)
            {
                writer.Write(destination[..bytesWritten]);
            }
            if (bytesConsumed > 0)
            {
                source = source[bytesConsumed..];
            }
        }
    }

    public void Dispose()
    {
    }
}
