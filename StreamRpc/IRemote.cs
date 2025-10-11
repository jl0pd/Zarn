namespace StreamRpc;

public interface IRemote<T> where T : class
{
    public T Value { get; }
}

internal sealed class Remote<T>(T value) : IRemote<T> where T : class
{
    public T Value { get; } = value;
}
