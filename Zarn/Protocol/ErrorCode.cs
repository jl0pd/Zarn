namespace Zarn.Protocol;

internal enum ErrorCode : byte
{
    Ok = 0,
    ProtocolMajorVersionMismatch = 1,
    ProtocolMinorVersionMismatch = 2,
    InvalidHeader = 3,
}
