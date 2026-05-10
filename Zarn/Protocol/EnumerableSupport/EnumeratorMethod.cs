namespace Zarn.Protocol.EnumerableSupport;

[Flags]
internal enum EnumeratorMethod : byte
{
    Error = 0,
    Dispose = 1,
    DisposeAsync = 2,
    MoveNext = 3,
    MoveNextAsync = 4,
    Reset = 5,
}
