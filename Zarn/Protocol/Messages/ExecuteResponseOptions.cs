namespace Zarn.Protocol.Messages;

[Flags]
internal enum ExecuteResponseOptions : byte
{
    None = 0,

    Success = 0x01,
    Compressed = 0x02,
}
