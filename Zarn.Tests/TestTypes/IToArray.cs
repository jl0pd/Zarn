using Zarn.Tests.Utils;

namespace Zarn.Tests.TestTypes;

public interface IToArray
{
    int[] ToArray(IEnumerable<int> source);

    ValueTask<int[]> ToArrayAsync(IAsyncEnumerable<int> source, CancellationToken cancellationToken);
}

public sealed class ToArray : IToArray
{
    public ValueTask<int[]> ToArrayAsync(IAsyncEnumerable<int> source, CancellationToken cancellationToken)
    {
        return source.ToArrayAsync(cancellationToken);
    }

    int[] IToArray.ToArray(IEnumerable<int> source)
    {
        return source.ToArray();
    }
}
