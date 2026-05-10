using System.Buffers;
using Zarn.Serialization.Serializers.Core;

namespace Zarn.Serialization;

public abstract class BinarySerializer<T> : BinarySerializer
{
    internal override byte[] TypePrefix => _typePrefix ??= GetTypePrefix(typeof(T));
    internal byte[]? _typePrefix;

    public override bool CanConvert(Type type) => type.IsAssignableTo(typeof(T));

    public sealed override void Serialize(object? value, Type type, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        if (!type.IsAssignableTo(typeof(T)))
        {
            throw new ArgumentException("Attempt to serialize different type", nameof(type));
        }
        Serialize((T)value!, writer, context);
    }

    public sealed override object? Deserialize(Type type, ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        if (!type.IsAssignableTo(typeof(T)))
        {
            throw new ArgumentException("Attempt to deserialize different type", nameof(type));
        }
        return Deserialize(ref source, context);
    }

    public abstract void Serialize(T value, IBufferWriter<byte> writer, BinarySerializationContext context);

    public abstract T Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context);
}
