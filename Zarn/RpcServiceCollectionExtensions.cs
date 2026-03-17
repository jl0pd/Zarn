using Microsoft.Extensions.DependencyInjection;

namespace Zarn;

public static class RpcServiceCollectionExtensions
{
    public static IServiceCollection AllowRemoteConnection<T>(this IServiceCollection serviceCollection)
    {
        var descr = serviceCollection.First(x => x.ServiceType == typeof(AllowedRemoteConnections));

        var conns = (AllowedRemoteConnections)descr.ImplementationInstance!;
        conns.Add(typeof(T));

        return serviceCollection;
    }

    public static T GetRemoteService<T>(this IServiceProvider services) where T : class
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.GetRequiredService<IRemote<T>>().Value;
    }
}
