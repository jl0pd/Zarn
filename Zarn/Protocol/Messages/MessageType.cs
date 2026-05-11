namespace Zarn.Protocol.Messages;

internal enum MessageType : byte
{
    Error = 0,

    HandshakeRequest = 1,
    HandshakeResponse = 2,

    ExecuteRequest = 3,
    ExecuteResponse = 4,
    ExecuteCancel = 5,
}
