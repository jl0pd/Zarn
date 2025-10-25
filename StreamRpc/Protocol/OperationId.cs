using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace StreamRpc.Protocol;

internal readonly record struct ObjectId(Guid Id)
{
    public static ObjectId GenObjectId()
    {
        Span<byte> bytes = stackalloc byte[OperationId.Size];
        Random.Shared.NextBytes(bytes[..(OperationId.Size - sizeof(short))]);
        return MemoryMarshal.Read<ObjectId>(bytes);
    }

    public static bool IsValid(ObjectId id)
    {
        return Unsafe.Add(ref Unsafe.As<ObjectId, short>(ref id), 7) == 0;
    }

    public override string ToString() => Id.ToString("n");
}

[StructLayout(LayoutKind.Explicit, Size = Size)]
internal readonly struct OperationId : IEquatable<OperationId>
{
    public const int Size = 16;

    [FieldOffset(0)]
    private readonly byte _firstByte;

    [FieldOffset(0)]
    private readonly Guid _asGuid;

    [field: FieldOffset(Size - sizeof(short))]
    public short Id { get; }

    public ObjectId Target
    {
        get
        {
            Span<byte> bytes = stackalloc byte[Size];
            AsSpan().CopyTo(bytes);
            bytes[Size - 1] = 0;
            bytes[Size - 2] = 0;
            return MemoryMarshal.Read<ObjectId>(bytes);
        }
    }

    public Guid AsGuid() => _asGuid;

    private ReadOnlySpan<byte> AsSpan() => MemoryMarshal.CreateReadOnlySpan(in _firstByte, Size);

    public OperationId(ObjectId target, short id)
    {
        Debug.Assert(ObjectId.IsValid(target));
        _asGuid = target.Id;
        Id = id;
    }

    public override string ToString()
    {
        return $"{Convert.ToHexString(AsSpan()[..(Size - sizeof(short))])}:{Id}";
    }

    public bool Equals(OperationId other) => _asGuid == other._asGuid; // uses vectorized comparison

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is OperationId id && Equals(id);

    public override int GetHashCode()
        => Unsafe.ReadUnaligned<int>(in Unsafe.Add(ref Unsafe.AsRef(in _firstByte), Size - sizeof(int)));

    public static bool operator ==(OperationId left, OperationId right) => left.Equals(right);
    public static bool operator !=(OperationId left, OperationId right) => !(left == right);
}
