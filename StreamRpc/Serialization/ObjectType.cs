namespace StreamRpc.Serialization;

[Flags]
internal enum ObjectType : byte
{
    // strictly speaking it doesn't have to be `[Flags]`, since only flag value
    // is whether it's an array, 6 higher bits aren't flags.
    Null = 0x00,
    Array = 0x01,
    String = 0x02,
    Int = 0x04,
    Type = 0x08,
    Byte = 0x10,
    Method = 0x20,
    CancellationToken = 0x40,
    Custom = 0x80,
}
