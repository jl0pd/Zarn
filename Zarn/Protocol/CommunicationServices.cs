namespace Zarn.Protocol;

internal static class CommunicationServices
{
    public static ObjectId ServerInstanceManagerId { get; } = new ObjectId(1, true);

    public static ObjectId ClientInstanceManagerId { get; } = new ObjectId(1, false);

    public static long LastReservedId { get; } = 10;

    public static Type[] Types { get; } =
    [
        typeof(IInstanceManager),
    ];
}
