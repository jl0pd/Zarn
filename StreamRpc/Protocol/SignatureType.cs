using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using StreamRpc.Serialization;

namespace StreamRpc.Protocol;

internal readonly struct SignatureType : IEquatable<SignatureType>, IBinarySerializable<SignatureType>
{
    public SignatureTypeKind Kind { get; }

    public Type? Type { get; }

    public int Index { get; }

    public SignatureType(Type type)
    {
        if (type.IsGenericParameter)
        {
            Index = type.GenericParameterPosition;
            Kind = type.IsGenericMethodParameter
                    ? SignatureTypeKind.MethodIndex
                    : SignatureTypeKind.TypeIndex;
        }
        else
        {
            Type = type;
            Index = -1;
            Kind = SignatureTypeKind.AssemblyQualified;
        }
    }

    public SignatureType(bool isMethod, int index)
    {
        Kind = isMethod ? SignatureTypeKind.MethodIndex : SignatureTypeKind.TypeIndex;
        Index = index;
    }

    [ExcludeFromCodeCoverage]
    public override string ToString()
    {
        return Kind switch
        {
            SignatureTypeKind.Uninitialized => "<error>",
            SignatureTypeKind.AssemblyQualified => Type?.Name ?? "<missing>",
            SignatureTypeKind.MethodIndex => "!!" + Index,
            SignatureTypeKind.TypeIndex => "!" + Index,
            _ => throw ThrowHelper.Unreachable,
        };
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is SignatureType type && Equals(type);

    public bool Equals(SignatureType other)
        => Kind == other.Kind
        && Index == other.Index
        && Type == other.Type;

    public override int GetHashCode()
        => throw ThrowHelper.Fail("This type shouldn't be used inside dictionaries");

    public static bool operator ==(SignatureType left, SignatureType right) => left.Equals(right);
    public static bool operator !=(SignatureType left, SignatureType right) => !(left == right);

    public static SignatureType Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        var kind = (SignatureTypeKind)context.Deserialize<byte>(ref source);
        switch (kind)
        {
            case SignatureTypeKind.MethodIndex:
                int index = context.Deserialize<int>(ref source);
                return new SignatureType(true, index);
            case SignatureTypeKind.TypeIndex:
                index = context.Deserialize<int>(ref source);
                return new SignatureType(false, index);
            case SignatureTypeKind.AssemblyQualified:
                var type = context.Deserialize<Type>(ref source);
                return new SignatureType(type);
            default:
                throw new InvalidDataException();
        }
    }

    public void Serialize(IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize((byte)Kind, writer);
        switch (Kind)
        {
            case SignatureTypeKind.AssemblyQualified:
                context.Serialize(Type, writer);
                break;
            case SignatureTypeKind.MethodIndex:
            case SignatureTypeKind.TypeIndex:
                context.Serialize(Index, writer);
                break;
            case SignatureTypeKind.Uninitialized:
                Debug.Fail("Uninitialized value should not be serialized");
                break;
        }
    }
}
