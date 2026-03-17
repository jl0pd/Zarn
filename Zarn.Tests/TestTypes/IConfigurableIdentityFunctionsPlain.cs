namespace Zarn.Tests.TestTypes;

public interface IConfigurableIdentityFunctionsPlain
{
    ValueTask SetIdentityFunctions(IIdentityFunctions functions);

    ValueTask<IIdentityFunctions> GetFunctions();

    string Id(string value);

    ValueTask<string> IdValueAsync(string value);

    Task<string> IdAsync(string value);
}

public sealed class ConfigurableIdentityFunctions : IConfigurableIdentityFunctionsPlain, IIdentityFunctions
{
    private IIdentityFunctions _impl = null!;

    public ValueTask<IIdentityFunctions> GetFunctions() => ValueTask.FromResult(_impl);

    public ValueTask SetIdentityFunctions(IIdentityFunctions functions)
    {
        _impl = functions;
        return ValueTask.CompletedTask;
    }

    public string Id(string value) => _impl.Id(value);

    public Task<string> IdAsync(string value) => _impl.IdAsync(value);

    public ValueTask<string> IdValueAsync(string value) => _impl.IdValueAsync(value);
}

