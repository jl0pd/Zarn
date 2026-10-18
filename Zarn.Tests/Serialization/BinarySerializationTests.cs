using System.Buffers;
using Zarn.Protocol;
using Zarn.Serialization;

namespace Zarn.Tests.Serialization;

[SuppressMessage("Design", "CA1024:Use properties where appropriate",
    Justification = "Methods for MemberData")]
public sealed class BinarySerializationTests
{
    private sealed class UnallowedType;

    private static void TestRoundtripCore<T>(T value, BinarySerializationContext context)
    {
        var writer = new ArrayBufferWriter<byte>();

        context.Serialize(value, writer);

        var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(writer.WrittenMemory));

        var result = context.Deserialize<T>(ref reader);

        Assert.Equal(0, reader.Remaining);
        Assert.Equal(value, result);
    }

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
    [MemberData(nameof(GetObjectId))]
    [MemberData(nameof(GetOperationId))]
    public void TestRoundtrip<T>(T value)
    {
        Assert.NotNull(value);

        var settings = new RpcSettings();
        var context = new BinarySerializationContext(settings);
        context.AllowType(value.GetType());
        context.AllowType(typeof(T));
        if (value is Type t)
        {
            context.AllowType(t);
        }

        TestRoundtripCore(value, context);
        TestRoundtripCore(new T[] { value, value }, context);
        TestRoundtripCore(new List<T[]> { new T[] { value }, new T[] { value, value } }, context);
        TestRoundtripCore(new List<T>[] { [value], [value, value] }, context);
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(List<>))]
    [InlineData(typeof(Dictionary<,>))]
    public void TestTypeRoundtrip(Type type)
    {
        // use separate method, because generic method resolves to private RuntimeType
        TestRoundtrip(type);
    }

    [Theory]
    [InlineData(typeof(UnallowedType))]
    [InlineData(typeof(List<UnallowedType>))]
    public void TestUnallowedTypeThrows(Type instanceType)
    {
        var settings = new RpcSettings();
        var context = new BinarySerializationContext(settings);

        var writer = new ArrayBufferWriter<byte>();
        context.Serialize(instanceType, writer);

        var ex = Record.Exception(() =>
        {
            var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(writer.WrittenMemory));
            context.Deserialize<Type>(ref reader);
        });

        Assert.IsType<InvalidOperationException>(ex);
    }

    [Fact]
    public void TestMalformedTypeThrows()
    {
        var settings = new RpcSettings();
        var context = new BinarySerializationContext(settings);

        var writer = new ArrayBufferWriter<byte>();
        context.Serialize("unallowed type, Asm=xxx", writer);

        var ex = Record.Exception(() =>
        {
            var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(writer.WrittenMemory));
            context.Deserialize<Type>(ref reader);
        });

        Assert.IsType<InvalidDataException>(ex);
    }

    public static IEnumerable<object[]> GetObjectId()
        => [[new ObjectId(42, true)]];

    public static IEnumerable<object[]> GetOperationId()
        => [[new OperationId(new ObjectId(42, false), 7)]];
}
