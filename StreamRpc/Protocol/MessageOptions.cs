namespace StreamRpc.Protocol;

[Flags]
internal enum ExecuteRequestOptions : byte
{
    None = 0,

    /// <summary>
    /// Target method is generic
    /// </summary>
    GenericMethod = 0x01,

    /// <summary>
    /// Target type is generic
    /// </summary>
    GenericType = 0x02,

    Reserved04 = 0x04,
    Reserved08 = 0x08,
    Reserved10 = 0x10,
    Reserved20 = 0x20,
    Reserved40 = 0x40,
    Reserved80 = 0x80,

    ReservedMask = 0xFC,
}

[Flags]
internal enum ExecuteResponseOptions
{
    None = 0,

    Success = 0x01,
}
