namespace StreamRpc.Protocol;

[Flags]
internal enum ExecuteRequestOptions : byte
{
    None = 0,

    /// <summary>
    /// Target method is generic
    /// </summary>
    GenericMethod = 0x01,
    Compressed = 0x02,
}

[Flags]
internal enum ExecuteResponseOptions : byte
{
    None = 0,

    Success = 0x01,
    Compressed = 0x02,
}
