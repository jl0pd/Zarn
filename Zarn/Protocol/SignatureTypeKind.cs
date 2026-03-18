namespace Zarn.Protocol;

internal enum SignatureTypeKind : byte
{
    /// <summary>
    /// Indicates that type was not filled.
    /// </summary>
    Uninitialized,

    /// <summary>
    /// Assembly qualified type name. System.Type.AssemblyQualifiedName except version part
    /// </summary>
    AssemblyQualified,

    /// <summary>
    /// Index of method generic parameter. !!0
    /// </summary>
    MethodIndex,

    /// <summary>
    /// Index of type generic parameter. !0
    /// </summary>
    TypeIndex,
}
