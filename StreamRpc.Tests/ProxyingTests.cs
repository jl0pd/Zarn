using StreamRpc.Tests.TestTypes;
using StreamRpc.Tests.Utils;

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

    [Fact]
    public Task GetSyncEnumerable() => RunConnectToServerTest<IEnumerableProvider, EnumerableProvider>(async (inst) =>
    {
        var result = inst.GetRange(5).ToArray();
        Assert.Equal([0, 1, 2, 3, 4], result);
    });

    [Fact]
    public Task GetAsyncEnumerable() => RunConnectToServerTest<IEnumerableProvider, EnumerableProvider>(async (inst) =>
    {
        var result = await inst.GetRangeAsync(5).ToArrayAsync();
        Assert.Equal([0, 1, 2, 3, 4], result);
    });

    [Fact]
    public Task SyncEnumerableToArray() => RunConnectToServerTest<IToArray, ToArray>(async (inst) =>
    {
        var result = inst.ToArray(Enumerable.Range(0, 5));
        Assert.Equal([0, 1, 2, 3, 4], result);
    });

    [Fact]
    public Task AsyncEnumerableToArray() => RunConnectToServerTest<IToArray, ToArray>(async (inst) =>
    {
        var result = await inst.ToArrayAsync(Enumerable.Range(0, 5).AsAsyncEnumerable(), CancellationToken.None);
        Assert.Equal([0, 1, 2, 3, 4], result);
    });

    [Fact]
    public Task ReturnedSeqThrowsOnCancel() => RunConnectToServerTest<IEnumerableProvider, EnumerableProvider>(async (inst) =>
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(0.2));

        int last = -1;
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in inst.GetInfiniteAsync().WithCancellation(cts.Token))
            {
                last = item;
            }
        });

        Assert.NotEqual(-1, last);
    });

    [Fact]
    public Task PassedSeqStopsOnCancel() => RunConnectToServerTest<IEnumerableProvider, EnumerableProvider>(async (inst) =>
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(0.2));

        var result = await inst.LastBeforeThrowAsync(new InfiniteAsyncEnumerable(), cts.Token);

        Assert.NotEqual(-1, result);
    });
}
