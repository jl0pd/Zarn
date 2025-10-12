namespace StreamRpc.Tests.TestTypes;

public interface IAdder
{
    ValueTask<int> AddValueAsync(int a, int b);
}

public sealed class Adder : IAdder
{
    public ValueTask<int> AddValueAsync(int a, int b)
    {
        return ValueTask.FromResult(a + b);
    }
}
