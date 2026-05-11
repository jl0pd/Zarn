namespace Zarn.Protocol.Messages;

internal enum MessageType : byte
{
    Error = 0,

    HandshakeRequest = 1,
    HandshakeResponse = 2,

    ExecuteRequest = 3,
    ExecuteResponse = 4,
    ExecuteCancelNotification = 5,

    CreateInstanceRequest = 6,
    CreateInstanceResponse = 7,

    ObjectCollectedNotification = 8,

    GetEnumeratorRequest = 9, // responded with CreateInstanceResponse
    CancelAsyncEnumeratorNotification = 10,
}
