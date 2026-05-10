namespace Zarn.Serialization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class BinarySerializerAttribute(Type serializerType) : Attribute
{
    public Type SerializerType { get; } = serializerType;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class BinarySerializerAttribute<T>() : BinarySerializerAttribute(typeof(T))
{
}
