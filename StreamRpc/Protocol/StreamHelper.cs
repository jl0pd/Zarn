using StreamRpc.Serialization;

namespace StreamRpc.Protocol;

// Note: methods from this class are manually inlined into ConnectionContext to remove
// allocations of StateMachineBox, caused by asynchronous completion of method.
// If method is updated here, it must be updated inside ConnectionContext and vise-versa
internal static class StreamHelper
{
    public static ReadOnlyMemory<byte> PrepareFirstChunk(long totalLength, ChunkedArrayPoolBufferWriter<byte>.Chunk chunk)
    {
        int skipBytes = PackedInt.MaxSize - PackedInt.GetRequiredSize(totalLength - PackedInt.MaxSize);
        PackedInt.Write(totalLength - skipBytes, chunk.Array.AsSpan(skipBytes));
        return chunk.Array.AsMemory(skipBytes, chunk.Written - skipBytes);
    }

    public static async ValueTask Send(Stream stream,
                                       ChunkedArrayPoolBufferWriter<byte> message,
                                       CancellationToken cancellationToken)
    {
        foreach (var chunk in message)
        {
            var memoryToWrite = chunk.ChunkIndex == 0
                                ? PrepareFirstChunk(message.TotalLength, chunk)
                                : chunk.WrittenMemory;
            await stream.WriteAsync(memoryToWrite, cancellationToken);
        }
        await stream.FlushAsync(cancellationToken);
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
            while (bytesRead < headerLength)
            {
                var read = await stream.ReadAsync(initialBuffer.AsMemory(bytesRead, headerLength - bytesRead), cancellationToken);
                if (read == 0)
                {
                    return false;
                }
                bytesRead += read;
            }
        }

        var messageLength = (int)PackedInt.Read(initialBuffer, out _);

        var payloadLength = messageLength - headerLength;
        var message = writer.GetMemory(payloadLength);
        bytesRead = 0;
        while (bytesRead < payloadLength)
        {
            var read = await stream.ReadAsync(message[bytesRead..payloadLength], cancellationToken);
            if (read == 0)
            {
                return false;
            }

            bytesRead += read;
        }
        writer.Advance(payloadLength);

        return true;
    }
}
