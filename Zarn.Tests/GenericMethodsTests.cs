using Zarn.Tests.TestTypes;
using Zarn.Tests.Utils;

namespace Zarn.Tests;

public sealed class GenericMethodsTests : RpcTestsBase
{
    [Fact]
    public Task TestParameterlessMethod() => RunConnectToServerTest<IGenericMethods, GenericMethods>(async inst =>
    {
        // also checks that reuse of GenericMethodInvokeTrampoline works correctly
        Assert.Equal("Int32", inst.FreeGeneric<int>());
        Assert.Equal("Int32", inst.FreeGeneric<int>());
        Assert.Equal("String", inst.FreeGeneric<string>());
        Assert.Equal("String", inst.FreeGeneric<string>());
        Assert.Equal("Single[]", inst.FreeGeneric<float[]>());
        Assert.Equal("Single[]", inst.FreeGeneric<float[]>());
    });

    [Fact]
    public Task TestDefault() => RunConnectToServerTest<IGenericMethods, GenericMethods>(async inst =>
    {
        Assert.Equal(0, inst.Default<int>());
        Assert.Equal(0f, inst.Default<float>());
        Assert.Null(inst.Default<string?>());
    });

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
        Assert.Equal((float[])[], await inst.IdAsync(Array.Empty<float>()));
    });

    [Fact]
    public Task TestReplicateToList() => RunConnectToServerTest<IGenericMethods, GenericMethods>(async inst =>
    {
        Assert.Equal(["asd", "asd", "asd"], await inst.ReplicateToListAsync("asd", 3));
    });

    [Fact]
    public Task TestReplicateToArray() => RunConnectToServerTest<IGenericMethods, GenericMethods>(async inst =>
    {
        Assert.Equal((string[])["asd", "asd", "asd"], await inst.ReplicateToArrayAsync("asd", 3));
    });

    [Fact]
    public Task TestToArray() => RunConnectToServerTest<IGenericMethods, GenericMethods>(async inst =>
    {
        Assert.Equal((int[])[0, 1, 2, 3, 4], await inst.ToArrayAsync(Enumerable.Range(0, 5).AsAsyncEnumerable()));
    });
}
