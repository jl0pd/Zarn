namespace StreamRpc.Tests.TestTypes;

public interface ICancellationFunctions
{
    public ValueTask<int> CountUntilCancelled(CancellationToken cancellationToken);
}

public sealed class CancellationFunctions : ICancellationFunctions
{
    public ValueTask<int> CountUntilCancelled(CancellationToken cancellationToken)
    {
        int i = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            i++;
        }

        return ValueTask.FromResult(i);
    }
}
