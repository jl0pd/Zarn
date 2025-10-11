using System.Buffers;
using System.Reflection;

namespace StreamRpc.Serialization.Serializers;

internal sealed class MethodInfoBinarySerializer : BinarySerializer<MethodInfo>
{
    public static MethodInfoBinarySerializer Instance { get; } = new();

    internal override byte[] TypePrefix { get; } = [(byte)ObjectType.Method];

    public override MethodInfo Deserialize(ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        var declaringType = context.Deserialize<Type>(ref source);
        var name = context.Deserialize<string>(ref source);
        bool isGeneric = context.Deserialize<bool>(ref source);

        Type[] genericArgs = [];
        if (isGeneric)
        {
            genericArgs = context.Deserialize<Type[]>(ref source);
        }

        var parameters = context.Deserialize<Type[]>(ref source);

        var method = declaringType.GetMethod(name, parameters) ?? throw new InvalidOperationException("Cannot find method");

        var result = genericArgs.Length > 0 ? method.MakeGenericMethod(genericArgs) : method;
        return result;
    }

    public override void Serialize(MethodInfo value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(value.DeclaringType, writer);
        context.Serialize(value.Name, writer);
        if (value.IsGenericMethod)
        {
            if (!value.IsConstructedGenericMethod)
            {
                throw new ArgumentException("Only constructed generic methods are supported", nameof(value));
            }
            context.Serialize(true, writer);
            context.Serialize(value.GetGenericArguments(), writer);
        }
        else
        {
            context.Serialize(false, writer);
        }

        var parameters = value.GetParameters().Select(p => p.ParameterType).ToArray();
        context.Serialize(parameters, writer);
    }
}
