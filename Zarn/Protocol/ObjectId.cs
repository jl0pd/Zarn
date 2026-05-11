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
