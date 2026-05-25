using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using Zarn.Serialization;

namespace Zarn.Protocol;

internal sealed record InterfaceDescriptor(SignatureType.Named Name, MethodSignature[] Methods) : IBinarySerializable<InterfaceDescriptor>
{
    private Type? _resolvedType;
    private MethodInfo?[]? _resolvedMethods;

    public static InterfaceDescriptor FromType(Type type)
    {
        Debug.Assert(type.IsInterface);

        var methods = type
                        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .Select(MethodSignature.FromMethod)
                        .ToArray();

        return new InterfaceDescriptor((SignatureType.Named)SignatureType.Create(type), methods);
    }

    public Type? ResolveType()
    {
        return _resolvedType ??= Type.GetType(Name.AssemblyQualifiedName);
    }

    public MethodInfo?[] ResolveMethods()
    {
        return _resolvedMethods ??= ResolveMethodsCore();
    }

    private  MethodInfo?[] ResolveMethodsCore()
    {
        var type = ResolveType()
            ?? throw new InvalidOperationException("Unable to resolve methods for missing type: " + Name.AssemblyQualifiedName);

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

    [ExcludeFromCodeCoverage]
    public override string ToString()
    {
        var sb = new StringBuilder(Name.ToString());

        foreach (var method in Methods)
        {
            sb.AppendLine();
            sb.Append("    ");
            sb.Append(method.ToString());
        }

        return sb.ToString();
    }

    public static InterfaceDescriptor Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        var name = context.Deserialize<SignatureType>(ref source);
        var methods = context.Deserialize<MethodSignature[]>(ref source);

        return new InterfaceDescriptor((SignatureType.Named)name, methods);
    }

    public void Serialize(IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(Name, writer);
        context.Serialize(Methods, writer);
    }

    public static HashSet<Type> CollectInterfaces(IEnumerable<Type> types)
    {
        var result = new HashSet<Type>();
        var queue = new Queue<Type>(types);

        while (queue.TryDequeue(out var type))
        {
            if (!type.IsInterface)
            {
                // handled in services.AllowRemoteConnection<T>();
                throw ThrowHelper.Unreachable;
            }

            if (!result.Add(type))
            {
                continue;
            }

            foreach (var method in type.GetMethods())
            {
                foreach (var param in method.GetParameters())
                {
                    if (param.ParameterType.IsInterface)
                    {
                        queue.Enqueue(param.ParameterType);
                    }
                }

                var retType = method.ReturnType;
                if (retType.IsInterface)
                {
                    queue.Enqueue(retType);
                }

                if (retType.IsConstructedGenericType)
                {
                    var genDef = retType.GetGenericTypeDefinition();
                    if (genDef == typeof(Task<>) || genDef == typeof(ValueTask<>))
                    {
                        var genArg = retType.GetGenericArguments()[0];
                        if (genArg.IsInterface)
                        {
                            queue.Enqueue(genArg);
                        }
                    }
                }
            }
        }

        return result;
    }

    public static InterfaceDescriptor[] CollectDescriptors(IEnumerable<Type> types)
    {
        return CollectInterfaces(types).Select(FromType).ToArray();
    }
}
