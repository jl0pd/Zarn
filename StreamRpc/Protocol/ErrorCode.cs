namespace StreamRpc.Protocol;

internal enum ErrorCode : byte
{
    Ok = 0,
    ProtocolVersionMismatch = 1,
    InvalidHeader = 2,
}
