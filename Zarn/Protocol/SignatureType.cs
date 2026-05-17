using System.Buffers;
using System.Diagnostics;
using Zarn.Serialization;
using Zarn.Serialization.Serializers.Core;

namespace Zarn.Protocol;

internal abstract class SignatureType : IEquatable<SignatureType>, IBinarySerializable<SignatureType>
{
    protected enum TypeKind : byte
    {
        Error = 0,
        TypeIndex = 1,
        MethodIndex = 2,
        Named = 3,
        Array = 4,
        Reference = 5,
        Pointer = 6,
    }

    public abstract override string ToString();

    protected abstract TypeKind Kind { get; }

    public abstract override int GetHashCode();

    public abstract bool Equals(SignatureType? other);

    public sealed override bool Equals(object? obj) => Equals(obj as SignatureType);

    public static bool operator ==(SignatureType? left, SignatureType? right) => left?.Equals(right) ?? right is null;

    public static bool operator !=(SignatureType? left, SignatureType? right) => !(left == right);

    public sealed class TypeIndex(int index) : SignatureType
    {
        public int Index => index;

        public override string ToString() => "!" + index;

        protected override TypeKind Kind => TypeKind.TypeIndex;

        public override int GetHashCode() => 1_000_000 + Index;

        public override bool Equals(SignatureType? other)
        {
            return other is TypeIndex t && t.Index == index;
        }
    }

    public sealed class MethodIndex(int index) : SignatureType
    {
        public int Index => index;

        public override string ToString() => "!!" + index;

        protected override TypeKind Kind => TypeKind.MethodIndex;

        public override int GetHashCode() => 2_000_000 + index;

        public override bool Equals(SignatureType? other)
        {
            return other is MethodIndex m && m.Index == index;
        }
    }

    public sealed class Array(SignatureType elementType) : SignatureType
    {
        public SignatureType ElementType => elementType;

        public override string ToString() => elementType + "[]";

        protected override TypeKind Kind => TypeKind.Array;

        public override int GetHashCode() => 3_000_000 + elementType.GetHashCode();

        public override bool Equals(SignatureType? other)
        {
            return other is Array a && a.ElementType == ElementType;
        }
    }

    public sealed class Pointer(SignatureType elementType) : SignatureType
    {
        public SignatureType ElementType => elementType;

        public override string ToString() => "*" + elementType;

        protected override TypeKind Kind => TypeKind.Pointer;

        public override int GetHashCode() => 4_000_000 + elementType.GetHashCode();

        public override bool Equals(SignatureType? other)
        {
            return other is Pointer a && a.ElementType == ElementType;
        }
    }

    public sealed class Reference(SignatureType elementType) : SignatureType
    {
        public SignatureType ElementType => elementType;

        public override string ToString() => "&" + elementType;

        protected override TypeKind Kind => TypeKind.Reference;

        public override int GetHashCode() => 5_000_000 + elementType.GetHashCode();

        public override bool Equals(SignatureType? other)
        {
            return other is Reference a && a.ElementType == ElementType;
        }
    }

    public sealed class Named(string assemblyQualifiedName, SignatureType[] genericParams) : SignatureType
    {
        public string AssemblyQualifiedName => assemblyQualifiedName;

        public SignatureType[] GenericParams => genericParams;

        public override string ToString()
        {
            if (GenericParams.Length == 0)
            {
                return AssemblyQualifiedName;
            }
            else
            {
                return AssemblyQualifiedName + "<" + string.Join(",", genericParams.AsEnumerable()) + ">";
            }
        }

        protected override TypeKind Kind => TypeKind.Named;

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(AssemblyQualifiedName);
            foreach (var item in genericParams)
            {
                hashCode.Add(item);
            }

            return hashCode.ToHashCode();
        }

        public override bool Equals(SignatureType? other)
        {
            return other is Named n
                && n.AssemblyQualifiedName == AssemblyQualifiedName
                && n.GenericParams.SequenceEqual(GenericParams);
        }
    }

    public static SignatureType Create(bool isMethod, int index)
    {
        return isMethod
                ? new MethodIndex(index)
                : new TypeIndex(index);
    }

    public static Named Create(string assemblyQualifiedName, SignatureType[] genericParams)
    {
        return new Named(assemblyQualifiedName, genericParams);
    }

    public static SignatureType Create(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (type.IsGenericTypeParameter)
        {
            return new TypeIndex(type.GenericParameterPosition);
        }
        else if (type.IsGenericMethodParameter)
        {
            return new MethodIndex(type.GenericParameterPosition);
        }
        else if (type.IsGenericType)
        {
            var args = type.GetGenericArguments();
            var aqn = type.GetGenericTypeDefinition().AssemblyQualifiedName;
            Debug.Assert(aqn is { });
            aqn = TypeBinarySerializer.RemoveVersion(aqn);
            return new Named(aqn, args.Select(Create).ToArray());
        }
        else if (type.IsArray)
        {
            if (!type.IsSZArray)
            {
                throw new NotSupportedException("Only single dimension zero-based arrays are supported");
            }

            return new Array(Create(type.GetElementType()!));
        }
        else if (type.IsByRef)
        {
            return new Reference(Create(type.GetElementType()!));
        }
        else if (type.IsPointer)
        {
            return new Pointer(Create(type.GetElementType()!));
        }
        else
        {
            var aqn = type.AssemblyQualifiedName;
            Debug.Assert(aqn is { });
            aqn = TypeBinarySerializer.RemoveVersion(aqn);
            return new Named(aqn, []);
        }
    }

    public static SignatureType Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        var kind = context.Deserialize<TypeKind>(ref source);
        switch (kind)
        {
            case TypeKind.TypeIndex:
                return new TypeIndex(context.Deserialize<int>(ref source));
            case TypeKind.MethodIndex:
                return new MethodIndex(context.Deserialize<int>(ref source));
            case TypeKind.Named:
                var name = context.Deserialize<string>(ref source);
                var pars = context.Deserialize<SignatureType[]>(ref source);
                return new Named(name, pars);
            case TypeKind.Array:
                return new Array(context.Deserialize<SignatureType>(ref source));
            case TypeKind.Reference:
                return new Reference(context.Deserialize<SignatureType>(ref source));
            case TypeKind.Pointer:
                return new Pointer(context.Deserialize<SignatureType>(ref source));
            default:
                throw new InvalidDataException();
        }
    }

    public void Serialize(IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(Kind, writer);
        switch (this)
        {
            case TypeIndex t:
                context.Serialize(t.Index, writer);
                break;
            case MethodIndex m:
                context.Serialize(m.Index, writer);
                break;
            case Array m:
                context.Serialize(m.ElementType, writer);
                break;
            case Reference m:
                context.Serialize(m.ElementType, writer);
                break;
            case Pointer m:
                context.Serialize(m.ElementType, writer);
                break;
            case Named n:
                context.Serialize(n.AssemblyQualifiedName, writer);
                context.Serialize(n.GenericParams, writer);
                break;
            default:
                throw ThrowHelper.Unreachable;
        }
    }
}
