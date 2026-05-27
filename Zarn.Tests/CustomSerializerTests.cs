using Zarn.Tests.TestTypes;

namespace Zarn.Tests;

public sealed class CustomSerializerTests : RpcTestsBase
{
    [Fact]
    public async Task TestCustomSerializer()
    {
        await RunConnectToServerTestCore<IPersonFactory, PersonFactory>(async impl =>
        {
            var result = await impl.CreatePerson("John", 21);
            Assert.Equal(new Person("John", 21), result);
        },
        new RpcSettings()
        {
            Serializers =
            {
                new PersonBinarySerializer(),
            }
        });
    }
}
