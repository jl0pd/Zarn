using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace StreamRpc.Protocol;

[StructLayout(LayoutKind.Explicit, Size = Size)]
internal readonly struct OperationId : IEquatable<OperationId>
{
    private const int Size = 16;

    [FieldOffset(0)]
    private readonly byte _firstByte;

    public Guid Target
    {
        get
        {
            Span<byte> bytes = stackalloc byte[Size];
            AsSpan().CopyTo(bytes);
            bytes[Size - 1] = 0;
            bytes[Size - 2] = 0;
            return MemoryMarshal.Read<Guid>(bytes);
        }
    }

    public Guid AsGuid() => Unsafe.ReadUnaligned<Guid>(in _firstByte);

    [field: FieldOffset(Size - sizeof(short))]
    public short Id { get; }

    private ReadOnlySpan<byte> AsSpan() => MemoryMarshal.CreateReadOnlySpan(in _firstByte, Size);

    public OperationId(Guid target, short id)
    {
        Unsafe.As<byte, Guid>(ref _firstByte) = target;
        Id = id;
    }

    public static Guid GenObjectId()
    {
        Span<byte> bytes = stackalloc byte[Size];
        Random.Shared.NextBytes(bytes[..(Size - sizeof(short))]);
        return MemoryMarshal.Read<Guid>(bytes);
    }

    public override string ToString()
    {
        return $"{Convert.ToHexString(AsSpan()[..(Size - sizeof(short))])}:{Id}";
    }

    public bool Equals(OperationId other) => AsSpan().SequenceEqual(other.AsSpan());

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is OperationId id && Equals(id);

    public override int GetHashCode() 
        => Unsafe.ReadUnaligned<int>(in Unsafe.Add(ref Unsafe.AsRef(in _firstByte), Size - sizeof(int)));

    public static bool operator ==(OperationId left, OperationId right) => left.Equals(right);
    public static bool operator !=(OperationId left, OperationId right) => !(left == right);
}
