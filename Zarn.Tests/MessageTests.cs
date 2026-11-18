using System.Buffers;
using Zarn.Collections;
using Zarn.Protocol;
using Zarn.Protocol.Messages;
using Zarn.Serialization;
using Zarn.Tests.TestTypes;

namespace Zarn.Tests;

public sealed class MessageTests
{
    private static async Task TestRoundtripCore<T>(T requestMessage) where T : struct, IMessageInternal<T>
    {
        var buffer = new ChunkedArrayPoolBufferWriter<byte>(128, 4096);
        var serializationContext = new BinarySerializationContext(new RpcSettings());

        var stream = new MemoryStream();

        await StreamHelper.Send(stream, requestMessage, buffer, serializationContext, TestContext.Current.CancellationToken);

        buffer.Reset();
        stream.Position = 0;

        bool readSuccess = await StreamHelper.Read(stream, new byte[PackedInt.MaxSize], buffer, TestContext.Current.CancellationToken);
        Assert.True(readSuccess);

        var readMessage = IMessageInternal<T>.Deserialize(buffer, serializationContext);
        Assert.Equivalent(requestMessage, readMessage, true);
    }

    [Fact]
    public async Task TestRequestRoundtrip()
    {
        await TestRoundtripCore(new HandshakeRequestMessage
        {
            ProtocolVersionMajor = 1,
            ProtocolVersionMinor = 1,
            AllowMinorVersionMismatch = false,
            Interfaces = [InterfaceDescriptor.FromType(typeof(IGreeter))],
            SupportedCompressions = ["gzip", "brotli"],
        });
    }

    [Fact]
    public async Task TestResponseRoundtrip()
    {
        await TestRoundtripCore(new HandshakeResponseMessage
        {
            ChosenCompression = "brotli",
            ErrorCode = ErrorCode.ProtocolMinorVersionMismatch,
            Interfaces = [InterfaceDescriptor.FromType(typeof(IGreeter))],
        });
    }

    public static TheoryData<int> MessageSizes() =>
    [
        0,
        1,
        62,
        63, // 63 + 1-byte prefix no longer fits in 1 byte
        64,
        8189,
        8190, // 8190 + 2-byte prefix no longer fits in 2 bytes
        8191,
        8192,
        100_000
    ];

    // sizes around PackedInt 1->2 byte (63) and 2->4 byte (8191) boundaries
    [Theory]
    [MemberData(nameof(MessageSizes))]
    public void TestMessageLengthOverflowToNextByte(int payloadSize)
    {
        var payload = new byte[payloadSize];
        Random.Shared.NextBytes(payload);

        var writer = new ChunkedArrayPoolBufferWriter<byte>(128, 4096);
        writer.Reserve(PackedInt.MaxSize);
        writer.Write(payload);

        var frame = StreamHelper.PrepareFirstChunk(writer.TotalLength, writer.FirstChunkRequired);

        int prefixSize = PackedInt.GetRequiredSize(payloadSize);
        Assert.Equal(prefixSize + writer.FirstChunkRequired.Written, frame.Length);

        var length = (int)PackedInt.Read(frame.Span, out int consumed);
        Assert.Equal(prefixSize, consumed);
        Assert.Equal(payloadSize, length);

        var firstChunkPayload = Math.Min(payloadSize, writer.FirstChunkRequired.Written);
        Assert.Equal(payload.AsSpan(0, firstChunkPayload).ToArray(), frame.Slice(consumed).ToArray());
    }

    [Fact]
    public async Task TestReadMultipleMessages()
    {
        int[] sizes = MessageSizes().Select(x => x.Data).ToArray();
        var payloads = new byte[sizes.Length][];
        var stream = new MemoryStream();

        for (int i = 0; i < sizes.Length; i++)
        {
            payloads[i] = new byte[sizes[i]];
            Random.Shared.NextBytes(payloads[i]);

            var writer = new ChunkedArrayPoolBufferWriter<byte>(128, 4096);
            writer.Reserve(PackedInt.MaxSize);
            writer.Write(payloads[i]);

            foreach (var chunk in writer)
            {
                var memoryToWrite = chunk.ChunkIndex == 0
                                    ? StreamHelper.PrepareFirstChunk(writer.TotalLength, chunk)
                                    : chunk.WrittenMemory;
                await stream.WriteAsync(memoryToWrite, TestContext.Current.CancellationToken);
            }
            await stream.FlushAsync(TestContext.Current.CancellationToken);
        }

        stream.Position = 0;
        foreach (var expected in payloads)
        {
            var writer = new ChunkedArrayPoolBufferWriter<byte>(128, 4096);
            bool success = await StreamHelper.Read(stream, new byte[PackedInt.MaxSize], writer, TestContext.Current.CancellationToken);
            Assert.True(success);
            var actual = writer.GetSequence().ToArray();
            Assert.Equal(expected, actual);
        }

        Assert.False(await StreamHelper.Read(stream,
                                             new byte[PackedInt.MaxSize],
                                             new ChunkedArrayPoolBufferWriter<byte>(128, 4096),
                                             TestContext.Current.CancellationToken));
    }
}
