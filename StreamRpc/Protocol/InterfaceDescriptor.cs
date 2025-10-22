using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using StreamRpc.Serialization;
using StreamRpc.Serialization.Serializers;

namespace StreamRpc.Protocol;

[BinarySerializer<InterfaceDescriptorBinarySerializer>]
internal sealed record InterfaceDescriptor(string AssemblyQualifiedName,
                                           int GenericParameterCount,
                                           MethodSignature[] Methods)
{
    private Type? _resolvedType;

    public static InterfaceDescriptor FromType(Type type)
    {
        Debug.Assert(type.IsInterface);
        Debug.Assert(!type.IsGenericType || type.IsGenericTypeDefinition, "Interface cannot be closed generic");

        var name = TypeBinarySerializer.RemoveVersion(type.AssemblyQualifiedName);
        Debug.Assert(name is { }, "Assembly qualified name is null only for generic type parameters");

        var parameters = type.IsGenericType ? type.GenericTypeArguments.Length : 0;

        var methods = type
                        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .Select(MethodSignature.FromMethod)
                        .ToArray();

        return new InterfaceDescriptor(name, parameters, methods);
    }

    public Type? ResolveType()
    {
        return _resolvedType ??= Type.GetType(AssemblyQualifiedName);
    }

    public MethodInfo?[] ResolveMethods()
    {
        var type = ResolveType()
            ?? throw new InvalidOperationException("Unable to resolve methods for missing type: " + AssemblyQualifiedName);

        var runtimeMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        var result = new MethodInfo?[Methods.Length];

        for (int i = 0; i < Methods.Length; i++)
        {
            for (int j = 0; j < runtimeMethods.Length; j++)
            {
                var m = runtimeMethods[j];
                if (m != null && Methods[i].Matches(m))
                {
                    result[i] = m;
                    runtimeMethods[j] = null!; // skip checks for already resolved methods
                    break;
                }
            }
        }

        return result;
    }

    public override string ToString()
    {
        var sb = new StringBuilder(AssemblyQualifiedName);

        if (GenericParameterCount > 0)
        {
            sb.Append('<');
            for (int i = 0; i < GenericParameterCount; i++)
            {
                sb.Append(',');
            }
            sb.Append('>');
        }

        foreach (var method in Methods)
        {
            sb.AppendLine();
            sb.Append("    ");
            sb.Append(method.ToString());
        }

        return sb.ToString();
    }
}

internal sealed class InterfaceDescriptorBinarySerializer : BinarySerializer<InterfaceDescriptor>
{
    public override InterfaceDescriptor Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        var name = context.Deserialize<string>(ref source);
        var count = context.Deserialize<int>(ref source);
        var methods = context.Deserialize<MethodSignature[]>(ref source);

        return new InterfaceDescriptor(name, count, methods);
    }

    public override void Serialize(InterfaceDescriptor value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(value.AssemblyQualifiedName, writer);
        context.Serialize(value.GenericParameterCount, writer);
        context.Serialize(value.Methods, writer);
    }
}
