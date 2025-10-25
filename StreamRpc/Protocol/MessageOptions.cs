namespace StreamRpc.Protocol;

[Flags]
internal enum ExecuteRequestOptions : byte
{
    None = 0,

    /// <summary>
    /// Target method is generic
    /// </summary>
    GenericMethod = 0x01,
}

[Flags]
internal enum ExecuteResponseOptions
{
    None = 0,

    Success = 0x01,
}
