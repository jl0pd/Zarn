using Microsoft.Extensions.DependencyInjection;

namespace Zarn;

public static class RpcServiceCollectionExtensions
{
    public static IServiceCollection AllowRemoteConnection<T>(this IServiceCollection serviceCollection)
    {
        return serviceCollection.AllowRemoteConnection(typeof(T));
    }

    public static IServiceCollection AllowRemoteConnection(this IServiceCollection serviceCollection, Type type)
    {
        ArgumentNullException.ThrowIfNull(serviceCollection);
        ArgumentNullException.ThrowIfNull(type);

        if (!type.IsInterface)
        {
            throw new ArgumentException("Only interfaces can be remotely connected", nameof(type));
        }

        var descr = serviceCollection.First(x => x.ServiceType == typeof(AllowedRemoteConnections));

        var conns = (AllowedRemoteConnections)descr.ImplementationInstance!;
        conns.Add(type);

        return serviceCollection;
    }

    public static T GetRemoteService<T>(this IServiceProvider services) where T : class
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.GetRequiredService<IRemote<T>>().Value;
    }
}
