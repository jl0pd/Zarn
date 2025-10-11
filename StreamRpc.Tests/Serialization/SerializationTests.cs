using StreamRpc.Serialization.Serializers;


namespace StreamRpc.Tests.Serialization;

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
}
