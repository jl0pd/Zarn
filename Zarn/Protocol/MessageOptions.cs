namespace Zarn.Protocol;

[Flags]
internal enum ExecuteRequestOptions : byte
{
    None = 0,
    Compressed = 0x01,
}

[Flags]
internal enum ExecuteResponseOptions : byte
{
    None = 0,

    Success = 0x01,
    Compressed = 0x02,
}
