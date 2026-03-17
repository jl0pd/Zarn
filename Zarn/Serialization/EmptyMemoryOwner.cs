using System.Buffers;

namespace Zarn.Serialization;

internal sealed class EmptyMemoryOwner<T> : IMemoryOwner<T>
{
    private bool _isDisposed;

    public Memory<T> Memory
    {
        get
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            return Memory<T>.Empty;
        }
    }

    public void Dispose() => _isDisposed = true;
}
