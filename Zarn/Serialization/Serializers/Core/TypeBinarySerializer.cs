using System.Buffers;
using System.ComponentModel;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;

namespace Zarn.Serialization.Serializers.Core;

internal sealed partial class TypeBinarySerializer : BinarySerializer<Type?>
{
    // statically cache most often used types. `new HasSet<>(otherSet, comparer)` have optimization when comparer matches
    private static readonly HashSet<string> s_builtinTypes = new(StringComparer.Ordinal)
    {
        RemoveVersion(typeof(bool).AssemblyQualifiedName!),

        RemoveVersion(typeof(sbyte).AssemblyQualifiedName!),
        RemoveVersion(typeof(byte).AssemblyQualifiedName!),
        RemoveVersion(typeof(short).AssemblyQualifiedName!),
        RemoveVersion(typeof(ushort).AssemblyQualifiedName!),
        RemoveVersion(typeof(int).AssemblyQualifiedName!),
        RemoveVersion(typeof(uint).AssemblyQualifiedName!),
        RemoveVersion(typeof(long).AssemblyQualifiedName!),
        RemoveVersion(typeof(ulong).AssemblyQualifiedName!),

        RemoveVersion(typeof(Half).AssemblyQualifiedName!),
        RemoveVersion(typeof(float).AssemblyQualifiedName!),
        RemoveVersion(typeof(double).AssemblyQualifiedName!),
        RemoveVersion(typeof(decimal).AssemblyQualifiedName!),

        RemoveVersion(typeof(char).AssemblyQualifiedName!),
        RemoveVersion(typeof(string).AssemblyQualifiedName!),

        RemoveVersion(typeof(Guid).AssemblyQualifiedName!),
    };

    internal override byte[] TypePrefix { get; } = [(byte)ObjectType.Type];

    private readonly HashSet<string> _allowedTypes;

    public TypeBinarySerializer()
    {
        _allowedTypes = new HashSet<string>(s_builtinTypes, s_builtinTypes.Comparer);
    }

    public bool AllowType(Type type)
    {
        var aqn = type.AssemblyQualifiedName ?? throw new ArgumentException("Type must have assembly name", nameof(type));
        return _allowedTypes.Add(RemoveVersion(aqn));
    }

    public static void SerializeCore(Type? value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        StringBinarySerializer.SerializeCore(RemoveVersion(value?.AssemblyQualifiedName), writer, context);
    }

    public override void Serialize(Type? value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        SerializeCore(value, writer, context);
    }

    [GeneratedRegex(@", Version=\d+\.\d+\.\d+\.\d+")]
    private static partial Regex VersionRegex { get; }

    [return: NotNullIfNotNull(nameof(typeName))]
    internal static string? RemoveVersion(string? typeName)
    {
        if (typeName is null)
        {
            return null;
        }

        return VersionRegex.Replace(typeName, "");
    }

    public override Type? Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        var name = StringBinarySerializer.Instance.Deserialize(ref source, context);
        if (name is null)
        {
            return null;
        }

        if (!_allowedTypes.Contains(name))
        {
            if (!TypeName.TryParse(name, out var parsedName))
            {
                throw new InvalidDataException("Invalid type name: " + name);
            }

            if (!TryFindUnrestrictedType(parsedName))
            {
                throw new InvalidOperationException("Attempt to deserialize restricted type: " + name);
            }
        }

        var result = Type.GetType(name) ?? throw new InvalidDataException("Failed to load type: " + name);
        return result;
    }

    [Description("Attribute is used to keep this comment in compiled library for thorough developer who uses decompiler. " +
                 "Does not cache into `_allowedTypes` to avoid uncontrolled memory growth. " +
                 "If profiler says that this method is in hot path, consider calling " +
                 "`BinarySerializationContext.AllowType` method or adding type to `RpcSettings.AllowedTypes`.")]
    private bool TryFindUnrestrictedType(TypeName name)
    {
        if (name.IsArray)
        {
            return TryFindUnrestrictedType(name.GetElementType());
        }
        else if (name.IsConstructedGenericType)
        {
            var typeDef = name.GetGenericTypeDefinition();
            if (!TryFindUnrestrictedType(typeDef))
            {
                return false;
            }

            var genArgs = name.GetGenericArguments();
            foreach (var genArg in genArgs)
            {
                if (!TryFindUnrestrictedType(genArg))
                {
                    return false;
                }
            }

            return true;
        }
        else
        {
            return _allowedTypes.Contains(name.AssemblyQualifiedName);
        }
    }
}
