using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace StreamRpc.Protocol;

[StructLayout(LayoutKind.Explicit, Size = Size)]
internal readonly struct ObjectId : IEquatable<ObjectId>
{
    public const int Size = 14;

    [FieldOffset(0)]
    private readonly byte _firstByte;

    public static ObjectId GenObjectId()
    {
        Span<byte> bytes = stackalloc byte[Size];
        Random.Shared.NextBytes(bytes);
        return MemoryMarshal.Read<ObjectId>(bytes);
    }

    private ReadOnlySpan<byte> AsSpan() => MemoryMarshal.CreateReadOnlySpan(in _firstByte, Size);

    public static bool IsValid(ObjectId id)
    {
        return Unsafe.Add(ref Unsafe.As<ObjectId, short>(ref id), 7) == 0;
    }

    public override string ToString() => Convert.ToHexString(AsSpan());

    public bool Equals(ObjectId other) => AsSpan().SequenceEqual(other.AsSpan());

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is ObjectId other && Equals(other);

    public override int GetHashCode() => Unsafe.ReadUnaligned<int>(in _firstByte);

    public static bool operator ==(ObjectId left, ObjectId right) => left.Equals(right);
    public static bool operator !=(ObjectId left, ObjectId right) => !(left == right);
}

[StructLayout(LayoutKind.Explicit, Size = Size)]
internal readonly struct OperationId : IEquatable<OperationId>
{
    public const int Size = 16;

    [FieldOffset(0)]
    private readonly Guid _asGuid;

    [FieldOffset(Size - sizeof(int))]
    private readonly int _hashCode;

    [field: FieldOffset(Size - sizeof(short))]
    public short Id { get; }

    [field: FieldOffset(0)]
    public ObjectId Target { get; }

    public Guid AsGuid() => _asGuid;

    public OperationId(ObjectId target, short id)
    {
        Debug.Assert(ObjectId.IsValid(target));
        Target = target;
        Id = id;
    }

    public override string ToString() => $"{Target}:{Id}";

    public bool Equals(OperationId other) => _asGuid == other._asGuid; // uses vectorized comparison

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is OperationId id && Equals(id);

    public override int GetHashCode() => _hashCode;

    public static bool operator ==(OperationId left, OperationId right) => left.Equals(right);
    public static bool operator !=(OperationId left, OperationId right) => !(left == right);
}
