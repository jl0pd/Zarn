namespace Zarn.Tests.TestTypes;

public interface IGreeter
{
    string GetGreeting(string name);

    ValueTask<string> GetGreetingValueAsync(string name);

    Task<string> GetGreetingAsync(string name);

    void SkipGreeting(string name);

    ValueTask SkipGreetingValueAsync(string name);
}

public sealed class Greeter : IGreeter
{
    public string GetGreeting(string name)
    {
        return $"Hello, {name}";
    }

    public async ValueTask<string> GetGreetingValueAsync(string name)
    {
        await Task.Yield();
        return $"Hello, {name}";
    }

    public async Task<string> GetGreetingAsync(string name)
    {
        await Task.Yield();
        return $"Hello, {name}";
    }

    public void SkipGreeting(string name)
    {
    }

    public Task<string> GetGreetingAsyncGeneric<T>(string name, T value)
    {
        return Task.FromResult($"Hello, {value} named {name}");
    }

    public ValueTask SkipGreetingValueAsync(string name)
    {
        return ValueTask.CompletedTask;
    }
}
