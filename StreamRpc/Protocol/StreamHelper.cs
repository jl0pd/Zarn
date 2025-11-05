using System.Diagnostics;
using StreamRpc.Serialization;

namespace StreamRpc.Protocol;

// Note: methods from this class are manually inlined into ConnectionContext to remove
// allocations of StateMachineBox, caused by asynchronous completion of method.
// If method is updated here, it must be updated inside ConnectionContext and vise-versa
internal static class StreamHelper
{
    public static ValueTask Send(Stream stream,
                                 ChunkedArrayPoolBufferWriter<byte> header,
                                 CancellationToken cancellationToken)
    {
        return Send(stream, header.TotalLength, header, null, cancellationToken);
    }

    public static async ValueTask Send(Stream stream,
                                       long length,
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
