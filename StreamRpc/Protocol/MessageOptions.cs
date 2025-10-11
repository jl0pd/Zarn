namespace StreamRpc.Protocol;

[Flags]
internal enum MessageOptions : byte
{
    None = 0,

    /// <summary>
    /// Message that support success indicates one
    /// </summary>
    Success = 0x01,

    /// <summary>
    /// Message was compressed
    /// </summary>
    Compressed = 0x02,

    /// <summary>
    /// Target method is generic
    /// </summary>
    GenericMethod = 0x04,

    /// <summary>
    /// Target type is generic
    /// </summary>
    GenericType = 0x08,

    Reserved10 = 0x10,
    Reserved20 = 0x20,
    Reserved40 = 0x40,

    /// <summary>
    /// Message may be split into several chunks and this flag indicates that this chunk was last.
    /// </summary>
    LastChunk = 0x80,

    ReservedMask = 0x70,
}
