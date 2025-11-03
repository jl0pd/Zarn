using StreamRpc.Tests.Utils;

namespace StreamRpc.Tests.TestTypes;

public interface IEnumerableProvider
{
    IEnumerable<int> GetRange(int count);

    IAsyncEnumerable<int> GetRangeAsync(int count);

    IAsyncEnumerable<int> GetInfiniteAsync();

    ValueTask<int> LastBeforeThrowAsync(IAsyncEnumerable<int> seq, CancellationToken cancellationToken);
}

public sealed class EnumerableProvider : IEnumerableProvider
{
    public IAsyncEnumerable<int> GetInfiniteAsync()
    {
        return new InfiniteAsyncEnumerable();
    }

    public IEnumerable<int> GetRange(int count)
    {
        return Enumerable.Range(0, count);
    }

    public IAsyncEnumerable<int> GetRangeAsync(int count)
    {
        return Enumerable.Range(0, count).AsAsyncEnumerable();
    }

    public async ValueTask<int> LastBeforeThrowAsync(IAsyncEnumerable<int> seq, CancellationToken cancellationToken)
    {
        int last = -1;
        try
        {
            await foreach (var item in seq.WithCancellation(cancellationToken))
            {
                last = item;
            }
        }
        catch
        {
        }

        return last;
    }
}
