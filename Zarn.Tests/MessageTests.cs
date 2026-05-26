using Zarn.Collections;
using Zarn.Protocol;
using Zarn.Protocol.Messages;
using Zarn.Serialization;
using Zarn.Tests.TestTypes;

namespace Zarn.Tests;

public sealed class MessageTests
{
    private async Task TestRoundtripCore<T>(T requestMessage) where T : struct, IMessageInternal<T>
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
}
