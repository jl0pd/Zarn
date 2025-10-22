using System.Buffers;
using StreamRpc.Serialization;

namespace StreamRpc.Tests.Serialization;

public sealed class BinarySerializationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(1f)]
    [InlineData(-1f)]
    [InlineData(1.0)]
    [InlineData(-1.0)]
    [InlineData("")]
    [InlineData("some text")]
    [InlineData(typeof(int))]
    [InlineData(typeof(List<>))]
    [InlineData(typeof(Dictionary<,>))]
    public void TestRoundtrip<T>(T value)
    {
        var settings = new RpcSettings();
        var context = new BinarySerializationContext(settings);

        var writer = new ArrayBufferWriter<byte>();

        context.Serialize(value, writer);

        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(writer.WrittenMemory));

        var result = context.Deserialize<T>(ref reader);

        Assert.Equal(0, reader.Remaining);
        Assert.Equal(value, result);
    }
}
