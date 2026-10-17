namespace Zarn.Serialization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
[SuppressMessage("Performance", "CA1813:Avoid unsealed attributes",
    Justification = "Child provides generic overload with compile-time check")]
public class BinarySerializerAttribute : Attribute
{
    public Type SerializerType { get; }

    public BinarySerializerAttribute(Type serializerType)
    {
        ArgumentNullException.ThrowIfNull(serializerType);

        if (!serializerType.IsAssignableTo(typeof(BinarySerializer)))
        {
            throw new ArgumentException($"Serializer must derive from {nameof(BinarySerializer)}");
        }

        SerializerType = serializerType;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class BinarySerializerAttribute<T>() : BinarySerializerAttribute(typeof(T))
where T : BinarySerializer
{
}
