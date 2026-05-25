namespace Zarn.Tests.TestTypes;

public interface IGreeterFactory
{
    public ValueTask<IGreeter> GetGreeter();
}

public sealed class GreeterFactory : IGreeterFactory
{
    public ValueTask<IGreeter> GetGreeter()
    {
        return ValueTask.FromResult<IGreeter>(new Greeter());
    }
}
