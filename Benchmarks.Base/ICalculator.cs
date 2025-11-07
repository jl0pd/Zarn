namespace Benchmarks;

public interface ICalculator
{
    ValueTask<int> Add(int left, int right);
}

public sealed class Calculator : ICalculator
{
    public ValueTask<int> Add(int left, int right)
    {
        return ValueTask.FromResult(left + right);
    }
}
