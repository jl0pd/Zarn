using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using Zarn.Serialization;

namespace Zarn.Protocol;

internal readonly struct OperationId(ObjectId target, short id) : IEquatable<OperationId>, IBinarySerializable<OperationId>
{
    public const int MinSize = 2;
    public const int MaxSize = sizeof(long) + sizeof(short);

    public int CompressedSize => PackedInt.GetRequiredSize(id) + Target.CompressedSize;

    public short Id => id;

    public ObjectId Target => target;

    public override string ToString() => $"{Target}:{Id}";

    public bool Equals(OperationId other) => Target == other.Target
                                          && Id == other.Id;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is OperationId id && Equals(id);

    public override int GetHashCode() => Id ^ Target.GetHashCode();

    public int Serialize(Span<byte> span)
    {
        int advance = Target.Serialize(span);
        return advance + PackedInt.Write(Id, span[advance..]);
    }

    public void Serialize(IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(Target, writer);
        context.Serialize(Id, writer);
    }

    public static OperationId Deserialize(ref SequenceReader<byte> reader, BinarySerializationContext context)
    {
        var target = context.Deserialize<ObjectId>(ref reader);
        var id = context.Deserialize<short>(ref reader);
        return new OperationId(target, id);
    }

    public static bool operator ==(OperationId left, OperationId right) => left.Equals(right);
    public static bool operator !=(OperationId left, OperationId right) => !(left == right);
}
