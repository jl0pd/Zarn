using System.Buffers;
using Zarn.Serialization;
using Zarn.Serialization.Serializers.Core;
using Zarn.Tests.Utils;


namespace Zarn.Tests.Serialization;

public class SerializationTests
{
    [Fact]
    public void RemoveVersion()
    {
        var typeName = "System.Int32, System.Private.CoreLib, Version=10.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e";
        var expected = "System.Int32, System.Private.CoreLib, Culture=neutral, PublicKeyToken=7cec85d7bea7798e";

        var actual = TypeBinarySerializer.RemoveVersion(typeName);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(65, 2)]
    [InlineData(8193, 4)]
    [InlineData(268_435_457, 8)]
    [InlineData(long.MaxValue, 9)]
    public void TestLongSerialization(long value, int byteLength)
    {
        var serializer = new PackedLongBinarySerializer();

        var ctx = new BinarySerializationContext(new RpcSettings());

        var writer = new ArrayBufferWriter<byte>(byteLength);
        serializer.Serialize(value, writer, ctx);

        Assert.Equal(byteLength, writer.WrittenCount);

        for (int i = 0; i < writer.WrittenCount; i++)
        {
            var seq = SequenceHelper.Split(writer.WrittenMemory, i + 1);

            var reader = new SequenceReader<byte>(seq);
            var result = serializer.Deserialize(ref reader, ctx);
            Assert.Equal(value, result);
            Assert.True(reader.End);
        }
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(65, 2)]
    [InlineData(8193, 4)]
    [InlineData(int.MaxValue, 8)]
    public void TestIntSerialization(int value, int byteLength)
    {
        var serializer = new PackedIntBinarySerializer();

        var ctx = new BinarySerializationContext(new RpcSettings());

        var writer = new ArrayBufferWriter<byte>(byteLength);
        serializer.Serialize(value, writer, ctx);

        Assert.Equal(byteLength, writer.WrittenCount);

        for (int i = 0; i < writer.WrittenCount; i++)
        {
            var seq = SequenceHelper.Split(writer.WrittenMemory, i + 1);

            var reader = new SequenceReader<byte>(seq);
            var result = serializer.Deserialize(ref reader, ctx);
            Assert.Equal(value, result);
            Assert.True(reader.End);
        }
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(65, 2)]
    [InlineData(8193, 4)]
    [InlineData(short.MaxValue, 4)]
    public void TestShortSerialization(short value, int byteLength)
    {
        var serializer = new PackedShortBinarySerializer();

        var ctx = new BinarySerializationContext(new RpcSettings());

        var writer = new ArrayBufferWriter<byte>(byteLength);
        serializer.Serialize(value, writer, ctx);

        Assert.Equal(byteLength, writer.WrittenCount);

        for (int i = 0; i < writer.WrittenCount; i++)
        {
            var seq = SequenceHelper.Split(writer.WrittenMemory, i + 1);

            var reader = new SequenceReader<byte>(seq);
            var result = serializer.Deserialize(ref reader, ctx);
            Assert.Equal(value, result);
            Assert.True(reader.End);
        }
    }
}
