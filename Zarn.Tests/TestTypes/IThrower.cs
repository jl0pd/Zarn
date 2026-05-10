namespace Zarn.Tests.TestTypes;

public interface IThrower
{
    public Task ThrowAsync(Type exceptionType, object?[] args);

    public void Throw(Type exceptionType, object?[] args);
}

public sealed class Thrower : IThrower
{
    public void Throw(Type exceptionType, object?[] args)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, args)!;
        throw exception;
    }

    public async Task ThrowAsync(Type exceptionType, object?[] args)
    {
        Throw(exceptionType, args);
    }
}
