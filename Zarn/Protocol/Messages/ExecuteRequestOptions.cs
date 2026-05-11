namespace Zarn.Protocol.Messages;

[Flags]
internal enum ExecuteRequestOptions : byte
{
    None = 0,
    Compressed = 0x01,
}
