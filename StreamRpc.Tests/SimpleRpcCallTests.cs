using StreamRpc.Tests.TestTypes;

namespace StreamRpc.Tests;

public sealed class SimpleRpcCallTests : RpcTestsBase
{
    [Fact]
    public Task SyncId() => RunConnectToServerTest<IIdentityFunctions, IdentityFunctions>(async (inst) =>
    {
        var value = Guid.NewGuid().ToString();
        Assert.Equal(value, inst.Id(value));
    });

    [Fact]
    public Task AsyncId() => RunConnectToServerTest<IIdentityFunctions, IdentityFunctions>(async (inst) =>
    {
        var value = Guid.NewGuid().ToString();
        Assert.Equal(value, await inst.IdAsync(value));
    });

    [Fact]
    public Task ValueAsyncId() => RunConnectToServerTest<IIdentityFunctions, IdentityFunctions>(async (inst) =>
    {
        var value = Guid.NewGuid().ToString();
        Assert.Equal(value, await inst.IdValueAsync(value));
    });

    [Fact]
    public Task SyncIgnore() => RunConnectToServerTest<IIgnoreFunctions, IgnoreFunctions>(async (inst) =>
    {
        var value = Guid.NewGuid().ToString();
        inst.Ignore(value);
    });

    [Fact]
    public Task AsyncIgnore() => RunConnectToServerTest<IIgnoreFunctions, IgnoreFunctions>(async (inst) =>
    {
        var value = Guid.NewGuid().ToString();
        await inst.IgnoreAsync(value);
    });

    [Fact]
    public Task ValueAsyncIgnore() => RunConnectToServerTest<IIgnoreFunctions, IgnoreFunctions>(async (inst) =>
    {
        var value = Guid.NewGuid().ToString();
        await inst.IgnoreValueAsync(value);
    });

    [Fact]
    public Task SyncReturn() => RunConnectToServerTest<IGreeter, Greeter>(async (inst) =>
    {
        Assert.Equal("Hello, John", inst.GetGreeting("John"));
    });

    [Fact]
    public Task ValueTaskReturn() => RunConnectToServerTest<IGreeter, Greeter>(async (inst) =>
    {
        Assert.Equal("Hello, John", await inst.GetGreetingValueAsync("John"));
    });

    [Fact]
    public Task TaskReturn() => RunConnectToServerTest<IGreeter, Greeter>(async (inst) =>
    {
        Assert.Equal("Hello, John", await inst.GetGreetingAsync("John"));
    });

    [Fact]
    public Task VoidReturn() => RunConnectToServerTest<IGreeter, Greeter>(async (inst) =>
    {
        inst.SkipGreeting("John");
    });
}
