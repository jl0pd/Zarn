using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace Zarn.Serialization.Serializers.Core;

internal sealed class TypeBinarySerializer : BinarySerializer<Type?>
{
    internal static TypeBinarySerializer Instance { get; } = new();

    internal override byte[] TypePrefix { get; } = [(byte)ObjectType.Type];

    public override void Serialize(Type? value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        StringBinarySerializer.Instance.Serialize(RemoveVersion(value?.AssemblyQualifiedName), writer, context);
    }

    [return: NotNullIfNotNull(nameof(typeName))]
    internal static string? RemoveVersion(string? typeName)
    {
        if (typeName is null)
        {
            return null;
        }

        var idx = typeName.IndexOf(", Version=");
        if (idx < 0)
        {
            return typeName;
        }

        var span = typeName.AsSpan();

        var versionPart = span[(idx + ", Version=".Length)..];
        var verReader = new SpanReader<char>(versionPart);
        verReader.ScanDigits();
        verReader.Expect('.');
        verReader.ScanDigits();
        verReader.Expect('.');
        verReader.ScanDigits();
        verReader.Expect('.');
        verReader.ScanDigits();

        int verLength = versionPart.Length - verReader.Remaining.Length;

        var newName = string.Concat(span[..idx], versionPart[verLength..]);
        return newName;
    }

    public override Type? Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        var name = StringBinarySerializer.Instance.Deserialize(ref source, context);
        if (name is null)
        {
            return null;
        }

        var result = Type.GetType(name) ?? throw new InvalidDataException("Failed to load type: " + name);
        return result;
    }
}
