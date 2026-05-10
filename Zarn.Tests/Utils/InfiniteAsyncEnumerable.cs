using System.Diagnostics;

namespace Zarn.Tests.Utils;

internal sealed class InfiniteAsyncEnumerable : IAsyncEnumerable<int>
{
    public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        Debug.Assert(cancellationToken.CanBeCanceled);
        return new Enumerator(cancellationToken);
    }

    private sealed class Enumerator(CancellationToken cancellationToken) : IAsyncEnumerator<int>
    {
        public int Current { get; set; }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask<bool> MoveNextAsync()
        {
            cancellationToken.ThrowIfCancellationRequested();
            Current++;
            return ValueTask.FromResult(true);
        }
    }
}
