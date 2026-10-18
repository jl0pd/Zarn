using System.Buffers;
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

        return new InterfaceDescriptor((SignatureType.Named)SignatureType.Create(type), methods)
        {
            _resolvedType = type,
        };
    }

    public Type GetStoredType()
    {
        if (_resolvedType is null)
        {
            ThrowHelper.ThrowUnreachable();
        }

        return _resolvedType;
    }

    public Type? ResolveType()
    {
        return _resolvedType ??= Type.GetType(Name.AssemblyQualifiedName);
    }

    public MethodInfo?[] ResolveMethods()
    {
        return _resolvedMethods ??= ResolveMethodsCore();
    }

    private MethodInfo?[] ResolveMethodsCore()
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

    private static readonly HashSet<Type> s_skipTypes = 
    [
        typeof(void),
        typeof(Task),
        typeof(ValueTask),
        typeof(Task<>),
        typeof(ValueTask<>),
    ];

    /// <summary>
    /// Collects interfaces for RPC type generation and types that are allowed to be deserialized.
    /// </summary>
    public static (HashSet<Type> Interfaces, HashSet<Type> Classes) CollectInterfaces(IEnumerable<Type> types)
    {
        var interfaces = new HashSet<Type>();
        var classes = new HashSet<Type>();
        var visited = new HashSet<Type>();

        foreach (var type in types)
        {
            AddType(type);
        }

        classes.ExceptWith(s_skipTypes);

        return (interfaces, classes);

        void AddType(Type type)
        {
            if (!visited.Add(type))
            {
                return;
            }

            if (type.IsGenericTypeDefinition)
            {
                if (type.IsInterface)
                {
                    interfaces.Add(type);
                    AddTypesFromMethods(type);
                }
                else
                {
                    classes.Add(type);
                }
            }
            else if (type.IsGenericType)
            {
                foreach (var genArg in type.GetGenericArguments())
                {
                    AddType(genArg);
                }

                AddType(type.GetGenericTypeDefinition());
            }
            else if (type.IsArray || type.IsPointer || type.IsByRef)
            {
                AddType(type.GetElementType()!);
            }
            else if (type.IsInterface)
            {
                interfaces.Add(type);
                AddTypesFromMethods(type);
            }
            else if (!type.IsGenericParameter)
            {
                classes.Add(type);
            }
        }

        void AddTypesFromMethods(Type type)
        {
            // do not scan classes, otherwise it would load entire BCL
            Debug.Assert(type.IsInterface);

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                AddType(method.ReturnType);
                foreach (var param in method.GetParameters())
                {
                    AddType(param.ParameterType);
                }
            }
        }
    }

    public static (InterfaceDescriptor[] Interfaces, HashSet<Type> Classes) CollectDescriptors(IEnumerable<Type> types)
    {
        var (interfaces, classes) = CollectInterfaces(types);
        var interfaceDescriptors = interfaces.Select(FromType).ToArray();
        return (interfaceDescriptors, classes);
    }
}
