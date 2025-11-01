using StreamRpc.Tests.TestTypes;

namespace StreamRpc.Tests;

public sealed class ProxyingTests : RpcTestsBase
{
    [Fact]
    public Task ReturnValueReferenceEqualToPassed() => RunConnectToServerTest<IConfigurableIdentityFunctionsPlain, ConfigurableIdentityFunctions>(async (inst) =>
    {
        var myIdentity = new FixedIdentityFunctions("");
        await inst.SetIdentityFunctions(myIdentity);

        var other = await inst.GetFunctions();
        Assert.Same(myIdentity, other);
    });

    [Fact]
    public Task ReverseCall() => RunConnectToServerTest<IConfigurableIdentityFunctionsPlain, ConfigurableIdentityFunctions>(async (inst) =>
    {
        string value = Guid.NewGuid().ToString();

        var myIdentity = new FixedIdentityFunctions(value);

        await inst.SetIdentityFunctions(myIdentity);

        var result = await inst.IdValueAsync("not a guid");

        Assert.Equal(value, result);
    });
}
