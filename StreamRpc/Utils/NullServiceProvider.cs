namespace StreamRpc.Utils;

internal sealed class NullServiceProvider : IServiceProvider
{
    public static readonly NullServiceProvider Instance = new();

    public object? GetService(Type serviceType)
    {
        return null;
    }
}
