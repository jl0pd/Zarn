namespace Zarn.Tests.Utils;

internal static class AsyncEnumerable
{
    public static IAsyncEnumerable<T> AsAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        if (source is IAsyncEnumerable<T> asyncEnum)
        {
            return asyncEnum;
        }
        else
        {
            return new AsyncOverSync<T>(source);
        }
    }

    public static async ValueTask<T[]> ToArrayAsync<T>(this IAsyncEnumerable<T> source, CancellationToken cancellationToken = default)
    {
        if (!(source is IEnumerable<T> seq && seq.TryGetNonEnumeratedCount(out int count)))
        {
            count = 0;
        }

        var result = new List<T>(count);

        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            result.Add(item);
        }

        return result.ToArray();
    }

    private sealed class AsyncOverSync<T>(IEnumerable<T> source) : IAsyncEnumerable<T>
    {
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new Enumerator(source.GetEnumerator(), cancellationToken);
        }

        private sealed class Enumerator(IEnumerator<T> enumerator, CancellationToken cancellationToken) : IAsyncEnumerator<T>
        {
            public T Current => enumerator.Current;

            public ValueTask DisposeAsync()
            {
                enumerator.Dispose();
                return ValueTask.CompletedTask;
            }

            public ValueTask<bool> MoveNextAsync()
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(enumerator.MoveNext());
            }
        }
    }
}
