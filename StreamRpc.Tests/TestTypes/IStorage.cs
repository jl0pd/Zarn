namespace StreamRpc.Tests.TestTypes;

public interface IStorage
{
    ValueTask StoreAsync(string value);

    ValueTask<string> GetStoredValue();
}

public sealed class Storage : IStorage
{
    private string _value = "";

    public ValueTask<string> GetStoredValue()
    {
        return ValueTask.FromResult(_value);
    }

    public ValueTask StoreAsync(string value)
    {
        _value = value;
        return ValueTask.CompletedTask;
    }
}
