using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace StreamRpc.Cil;

internal sealed class MetadataTokenFormatter : ISignatureTypeProvider<string, ImmutableArray<string>>
{
    public static readonly MetadataTokenFormatter Instance = new();

    public static string Format(MetadataReader reader, EntityHandle handle)
    {
        return Format(reader, handle, ImmutableArray<string>.Empty);
    }

    public static string Format(MetadataReader reader, EntityHandle handle, ImmutableArray<string> genericContext)
    {
        return handle.Kind switch
        {
            HandleKind.TypeDefinition => FormatTypeDef(reader, handle),
            HandleKind.TypeReference => FormatTypeRef(reader, handle),
            HandleKind.TypeSpecification => FormatTypeSpec(reader, handle, genericContext),
            HandleKind.MemberReference => FormatMemberRef(reader, handle, genericContext),
            HandleKind.MethodSpecification => FormatMethodSpec(reader, handle),
            HandleKind.MethodDefinition => FormatMethodDef(reader, handle, genericContext),
            _ => throw new NotImplementedException(),
        };
    }

    private static string FormatMethodDef(MetadataReader reader, EntityHandle handle, ImmutableArray<string> genericContext)
    {
        var methodDef = reader.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(MetadataTokens.GetRowNumber(handle)));

        string parent = Format(reader, methodDef.GetDeclaringType());
        string name = reader.GetString(methodDef.Name);
        var signature = methodDef.DecodeSignature(Instance, ImmutableArray<string>.Empty);

        string gen = "";

        if (signature.Header.IsGeneric)
        {
            gen = "<" + string.Join(", ", genericContext) + ">";
        }

        var inst = signature.Header.IsInstance ? "instance " : "";

        return inst + signature.ReturnType + " " + parent + "::" + name + gen + "(" + string.Join(", ", signature.ParameterTypes) + ")";
    }

    private static string FormatMethodSpec(MetadataReader reader, EntityHandle handle)
    {
        var methodSpec = reader.GetMethodSpecification(MetadataTokens.MethodSpecificationHandle(MetadataTokens.GetRowNumber(handle)));

        var context = methodSpec.DecodeSignature(Instance, ImmutableArray<string>.Empty);

        var method = Format(reader, methodSpec.Method, context);

        return method;
    }

    private static string FormatMemberRef(MetadataReader reader, EntityHandle handle, ImmutableArray<string> genericContext)
    {
        var memberRef = reader.GetMemberReference(MetadataTokens.MemberReferenceHandle(MetadataTokens.GetRowNumber(handle)));

        switch (memberRef.GetKind())
        {
            case MemberReferenceKind.Method:
                string parent = Format(reader, memberRef.Parent);
                string name = reader.GetString(memberRef.Name);
                var signature = memberRef.DecodeMethodSignature(Instance, ImmutableArray<string>.Empty);

                string gen = "";

                if (signature.Header.IsGeneric)
                {
                    gen = "<" + string.Join(", ", genericContext) + ">";
                }

                var inst = signature.Header.IsInstance ? "instance " : "";

                return inst + signature.ReturnType + " " + parent + "::" + name + gen + "(" + string.Join(", ", signature.ParameterTypes) + ")";

            default:
                throw new NotImplementedException();
        }
    }

    private static string FormatTypeSpec(MetadataReader reader, EntityHandle handle, ImmutableArray<string> genericContext)
    {
        var typeSpec = reader.GetTypeSpecification(MetadataTokens.TypeSpecificationHandle(MetadataTokens.GetRowNumber(handle)));

        var type = typeSpec.DecodeSignature(Instance, genericContext);

        return type;
    }

    private static string FormatTypeDef(MetadataReader reader, EntityHandle handle)
    {
        return Instance.GetTypeFromDefinition(reader, MetadataTokens.TypeDefinitionHandle(MetadataTokens.GetRowNumber(handle)), 0);
    }

    private static string FormatTypeRef(MetadataReader reader, EntityHandle handle)
    {
        return Instance.GetTypeFromReference(reader, MetadataTokens.TypeReferenceHandle(MetadataTokens.GetRowNumber(handle)), 0);
    }

    public string GetArrayType(string elementType, ArrayShape shape)
    {
        throw new NotImplementedException();
    }

    public string GetByReferenceType(string elementType)
    {
        return elementType + "&";
    }

    public string GetFunctionPointerType(MethodSignature<string> signature)
    {
        throw new NotImplementedException();
    }

    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
    {
        return genericType + "<" + string.Join(", ", typeArguments) + ">";
    }

    public string GetGenericMethodParameter(ImmutableArray<string> genericContext, int index)
    {
        if (genericContext.IsDefaultOrEmpty)
        {
            return "!!" + index;
        }
        else
        {
            return genericContext[index];
        }
    }

    public string GetGenericTypeParameter(ImmutableArray<string> genericContext, int index)
    {
        if (genericContext.IsDefaultOrEmpty)
        {
            return "!" + index;
        }
        else
        {
            return genericContext[index];
        }
    }

    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired)
    {
        throw new NotImplementedException();
    }

    public string GetPinnedType(string elementType)
    {
        return "pinned " + elementType;
    }

    public string GetPointerType(string elementType)
    {
        return elementType + "*";
    }

    public string GetPrimitiveType(PrimitiveTypeCode typeCode)
    {
        return typeCode switch
        {
            PrimitiveTypeCode.Void => "void",
            PrimitiveTypeCode.Boolean => "bool",
            PrimitiveTypeCode.Char => "char",
            PrimitiveTypeCode.SByte => "int8",
            PrimitiveTypeCode.Byte => "uint8",
            PrimitiveTypeCode.Int16 => "int16",
            PrimitiveTypeCode.UInt16 => "uint16",
            PrimitiveTypeCode.Int32 => "int32",
            PrimitiveTypeCode.UInt32 => "uint32",
            PrimitiveTypeCode.Int64 => "int64",
            PrimitiveTypeCode.UInt64 => "uint64",
            PrimitiveTypeCode.Single => "float32",
            PrimitiveTypeCode.Double => "float64",
            PrimitiveTypeCode.String => "string",
            PrimitiveTypeCode.IntPtr => "native int",
            PrimitiveTypeCode.UIntPtr => "native uint",
            PrimitiveTypeCode.Object => "object",
            PrimitiveTypeCode.TypedReference
            or _ => throw new NotImplementedException(),
        };
    }

    public string GetSZArrayType(string elementType)
    {
        return elementType + "[]";
    }

    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var typeDef = reader.GetTypeDefinition(handle);
        var kind = reader.ResolveSignatureTypeKind(handle, rawTypeKind);

        var name = reader.GetString(typeDef.Name);

        if (typeDef.Namespace.IsNil)
        {
            return name;
        }
        else
        {
            var ns = reader.GetString(typeDef.Namespace);
            return ns + "." + name;
        }
    }

    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var typeRef = reader.GetTypeReference(handle);

        var name = reader.GetString(typeRef.Name);

        if (typeRef.Namespace.IsNil)
        {
            return name;
        }
        else
        {
            var ns = reader.GetString(typeRef.Namespace);
            return ns + "." + name;
        }
    }

    public string GetTypeFromSpecification(MetadataReader reader, ImmutableArray<string> genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
    {
        throw new NotImplementedException();
    }
}
