namespace StreamRpc.Serialization;

public sealed class BinarySerializationSettings
{
    public IList<BinarySerializer> Serializers { get; } = new List<BinarySerializer>();

    public MemoryProvider? MemoryProvider { get; set; }

    public IList<Type> TransparentExceptions { get; } = new List<Type>
    {
        typeof(Exception),
        typeof(InvalidOperationException),
        typeof(ArgumentException),
        typeof(ArgumentNullException),
        typeof(ArgumentOutOfRangeException),
    };
}
