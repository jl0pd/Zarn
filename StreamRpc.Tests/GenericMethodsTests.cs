using StreamRpc.Tests.TestTypes;

namespace StreamRpc.Tests;

public sealed class GenericMethodsTests : RpcTestsBase
{
    [Fact]
    public Task TestId() => RunConnectToServerTest<IGenericMethods, GenericMethods>(async inst =>
    {
        Assert.Equal("Int32", inst.FreeGeneric<int>());
        Assert.Equal("String", inst.FreeGeneric<string>());
        Assert.Equal("Single[]", inst.FreeGeneric<float[]>());
    });

    /*
    [Fact]
    public Task TestId() => RunConnectToServerTest<IGenericMethods, GenericMethods>(async inst =>
    {
        Assert.Equal(1, inst.Id(1));
        Assert.Equal("", inst.Id(""));
        Assert.Equal([], inst.Id(Array.Empty<float>()));
    });

    [Fact]
    public Task TestIdAsync() => RunConnectToServerTest<IGenericMethods, GenericMethods>(async inst =>
    {
        Assert.Equal(1, await inst.IdAsync(1));
        Assert.Equal("", await inst.IdAsync(""));
        Assert.Equal([], await inst.IdAsync(Array.Empty<float>()));
    });
    */
}
