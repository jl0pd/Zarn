using System.Runtime.CompilerServices;
using Zarn.Tests.TestTypes;
using Zarn.Tests.Utils;

namespace Zarn.Tests;

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
        var result = await inst.ToArrayAsync(Enumerable.Range(0, 5).AsAsyncEnumerable(), TestContext.Current.CancellationToken);
        Assert.Equal([0, 1, 2, 3, 4], result);
    });

    [Fact]
    public Task ReturnedSeqThrowsOnCancel() => RunConnectToServerTest<IEnumerableProvider, EnumerableProvider>(async (inst) =>
    {
        using var cts = new CancellationTokenSource();

        bool enumerated = false;
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var item in inst.GetInfiniteAsync().WithCancellation(cts.Token))
            {
                enumerated = true;
                cts.Cancel();
            }
        });

        Assert.True(enumerated);
    });

    [Fact]
    public Task PassedSeqStopsOnCancel() => RunConnectToServerTest<IEnumerableProvider, EnumerableProvider>(async (inst) =>
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(0.2));

        var ctsBox = new StrongBox<CancellationTokenSource?>(cts);
        var sourceSeq = new InfiniteAsyncEnumerable();

        var cancellable = sourceSeq.Select(x =>
        {
            Interlocked.Exchange(ref ctsBox.Value, null)?.Cancel();
            return x;
        });

        var result = await inst.LastBeforeThrowAsync(cancellable, cts.Token);

        Assert.NotEqual(-1, result);
    });

    [Fact]
    public Task CallToReturnedInterface() => RunConnectToServerTest<IGreeterFactory, GreeterFactory>(async (inst) =>
    {
        var greeter = await inst.GetGreeter();
        var result = await greeter.GetGreetingValueAsync("World");
        Assert.Equal("Hello, World", result);
    });
}
