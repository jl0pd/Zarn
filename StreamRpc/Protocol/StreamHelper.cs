using System.Diagnostics;
using StreamRpc.Serialization;

namespace StreamRpc.Protocol;

internal static class StreamHelper
{
    public static ValueTask Send(Stream stream,
                                 MessageOptions options,
                                 ChunkedArrayPoolBufferWriter<byte> header,
                                 CancellationToken cancellationToken)
    {
        return Send(stream, header.TotalLength, options, header, null, cancellationToken);
    }

    public static async ValueTask Send(Stream stream,
                                       long length,
                                       MessageOptions options,
                                       ChunkedArrayPoolBufferWriter<byte> header,
                                       ChunkedArrayPoolBufferWriter<byte>? body,
                                       CancellationToken cancellationToken)
    {
        int chunkId = 0;
        foreach (var chunk in header)
        {
            ReadOnlyMemory<byte> memoryToWrite;
            if (chunkId == 0)
            {
                int msgBytesLength = PackedInt.GetRequiredSize(length - PackedInt.MaxSize);
                int skipBytes = PackedInt.MaxSize - msgBytesLength;
                int written = PackedInt.Write(length - skipBytes, chunk.Array.AsSpan(skipBytes));
                Debug.Assert(written == msgBytesLength);

                chunk.Array[PackedInt.MaxSize] = (byte)options;
                memoryToWrite = chunk.Array.AsMemory(skipBytes, chunk.Written - skipBytes);
            }
            else
            {
                memoryToWrite = chunk.WrittenMemory;
            }

            await stream.WriteAsync(memoryToWrite, cancellationToken);

            chunkId++;
        }

        if (body is { })
        {
            foreach (var chunk in body)
            {
                await stream.WriteAsync(chunk.WrittenMemory, cancellationToken);
            }
        }
    }

    public static async ValueTask<bool> Read(Stream stream,
                                             byte[] initialBuffer,
                                             ChunkedArrayPoolBufferWriter<byte> writer,
                                             CancellationToken cancellationToken)
    {
        int bytesRead = await stream.ReadAsync(initialBuffer.AsMemory(0, 1), cancellationToken);
        if (bytesRead == 0)
        {
            return false;
        }

        var headerLength = PackedInt.GetConsumedBytes(initialBuffer[0]);
        if (headerLength > 1)
        {
            await stream.ReadExactlyAsync(initialBuffer.AsMemory(1, headerLength - 1), cancellationToken);
            bytesRead += headerLength - 1;
        }

        var messageLength = (int)PackedInt.Read(initialBuffer, out _);

        var message = writer.GetMemory(messageLength - headerLength);
        await stream.ReadExactlyAsync(message[..(messageLength - headerLength)], cancellationToken);
        writer.Advance(messageLength - headerLength);

        return true;
    }
}
