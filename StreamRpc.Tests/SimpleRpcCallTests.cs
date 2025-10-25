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
    public Task PassCancelledToken() => RunConnectToServerTest<ICancellationFunctions, CancellationFunctions>(async (inst) =>
    {
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await inst.CountUntilCancelled(new CancellationToken(true));
        });
    });

    [Fact]
    public Task CancelTokenAfterSomeTime() => RunConnectToServerTest<ICancellationFunctions, CancellationFunctions>(async (inst) =>
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(0.2));
        var result = await inst.CountUntilCancelled(cts.Token);
        Assert.NotEqual(0, result);
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

    [Fact]
    public Task SameObjectIsUsed() => RunConnectToServerTest<IStorage, Storage>(async (inst) =>
    {
        var value = Guid.NewGuid().ToString("n");
        await inst.StoreAsync(value);

        var result = await inst.GetStoredValue();

        Assert.Equal(value, result);
    });
}
