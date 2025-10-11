namespace StreamRpc.Tests.TestTypes;

public interface IIgnoreFunctions
{
    void Ignore(string value);

    ValueTask IgnoreValueAsync(string value);

    Task IgnoreAsync(string value);
}

public sealed class IgnoreFunctions : IIgnoreFunctions
{
    public void Ignore(string value)
    {
    }

    public Task IgnoreAsync(string value) => Task.CompletedTask;

    public ValueTask IgnoreValueAsync(string value) => ValueTask.CompletedTask;
}
