using System.Buffers;

namespace StreamRpc.Serialization;

internal sealed class ArrayMemoryOwner<T>(T[]? array) : IMemoryOwner<T>
{
    public Memory<T> Memory
    {
        get
        {
            ObjectDisposedException.ThrowIf(array is null, this);
            return array;
        }
    }

    public void Dispose() => array = null;
}
