using System.Buffers;
using StreamRpc.Serialization.Serializers;

namespace StreamRpc.Serialization;

public abstract class BinarySerializer<T> : BinarySerializer
{
    byte[]? _typePrefix;

    internal override byte[] TypePrefix
    {
        get
        {
            if (_typePrefix is null)
            {
                var writer = new ArrayBufferWriter<byte>(256);
                writer.GetSpan()[0] = (byte)ObjectType.Custom;
                writer.Advance(1);
                TypeBinarySerializer.Instance.Serialize(typeof(T), writer, null!);
                _typePrefix = writer.WrittenSpan.ToArray();
            }

            return _typePrefix;
        }
    }

    public override bool CanConvert(Type type) => type.IsAssignableTo(typeof(T));

    public sealed override void Serialize(object? value, Type type, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        if (!type.IsAssignableTo(typeof(T)))
        {
            throw new ArgumentException("Attempt to serialize different type", nameof(type));
        }
        Serialize((T)value!, writer, context);
    }

    public sealed override object? Deserialize(Type type, ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context)
    {
        if (!type.IsAssignableTo(typeof(T)))
        {
            throw new ArgumentException("Attempt to deserialize different type", nameof(type));
        }
        return Deserialize(ref source, context);
    }

    public abstract void Serialize(T value, IBufferWriter<byte> writer, BinarySerializationContext context);

    public abstract T Deserialize(ref ReadOnlySequenceReader<byte> source, BinarySerializationContext context);
}
