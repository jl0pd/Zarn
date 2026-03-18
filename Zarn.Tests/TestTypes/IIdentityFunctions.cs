namespace Zarn.Tests.TestTypes;

public interface IIdentityFunctions
{
    string Id(string value);

    ValueTask<string> IdValueAsync(string value);

    Task<string> IdAsync(string value);
}

public sealed class IdentityFunctions : IIdentityFunctions
{
    public string Id(string value) => value;

    public Task<string> IdAsync(string value) => Task.FromResult(value);

    public ValueTask<string> IdValueAsync(string value) => ValueTask.FromResult(value);
}

public sealed class FixedIdentityFunctions(string returnValue) : IIdentityFunctions
{
    public string Id(string value) => returnValue;

    public Task<string> IdAsync(string value) => Task.FromResult(returnValue);

    public ValueTask<string> IdValueAsync(string value) => ValueTask.FromResult(returnValue);
}
