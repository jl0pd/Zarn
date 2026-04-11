using BenchmarkDotNet.Running;

public static class Program
{
    public static async Task Main(string[] args)
    {
        BenchmarkRunner.Run(typeof(Program).Assembly);
    }
}
