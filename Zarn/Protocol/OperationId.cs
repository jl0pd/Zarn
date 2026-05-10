using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Zarn.Serialization;

namespace Zarn.Protocol;

internal readonly struct ObjectId : IEquatable<ObjectId>, IBinarySerializable<ObjectId>
{
    public const int MinSize = 1;
    public const int MaxSize = sizeof(long);

    private readonly long _value;

    public bool IsServer => _value % 2 != 0;

    public ObjectId(long value, bool isServer) => _value = (value << 1) | (isServer ? 1L : 0L);

    private ObjectId(long value) => _value = value;

    public override string ToString() => _value.ToString(CultureInfo.InvariantCulture);

    public bool Equals(ObjectId other) => _value == other._value;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is ObjectId other && Equals(other);

    public override int GetHashCode() => _value.GetHashCode();

    public int CompressedSize => PackedInt.GetRequiredSize(_value);

    public int Serialize(Span<byte> span)
    {
        return PackedInt.Write(_value, span);
    }

    public void Serialize(IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        var span = writer.GetSpan(PackedInt.MaxSize);
        int written = PackedInt.Write(_value, span);
        writer.Advance(written);
    }

    public static ObjectId Deserialize(ref SequenceReader<byte> reader, BinarySerializationContext context)
    {
        return new ObjectId(context.Deserialize<long>(ref reader));
    }

    public static bool operator ==(ObjectId left, ObjectId right) => left.Equals(right);
    public static bool operator !=(ObjectId left, ObjectId right) => !(left == right);
}

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
