namespace Zarn.Protocol;

internal static class CommunicationServices
{
    public static ObjectId InstanceManager { get; } = ObjectId.FromSpan("InstanceMgr---"u8);

    public static Type[] Types { get; } =
    [
        typeof(IInstanceManager),
    ];
}
