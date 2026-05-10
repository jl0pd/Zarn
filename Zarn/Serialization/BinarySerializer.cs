using System.Buffers;
using Zarn.Serialization.Serializers.Core;

namespace Zarn.Serialization;

public abstract class BinarySerializer
{
    internal abstract byte[] TypePrefix { get; }

    internal static byte[] GetTypePrefix(Type type)
    {
        var writer = new ArrayBufferWriter<byte>(256);
        writer.GetSpan()[0] = (byte)ObjectType.Custom;
        writer.Advance(1);
        TypeBinarySerializer.Instance.Serialize(type, writer, null!);
        return writer.WrittenSpan.ToArray();
    }

    public abstract bool CanConvert(Type type);

    public abstract void Serialize(object? value, Type type, IBufferWriter<byte> writer, BinarySerializationContext context);

    public abstract object? Deserialize(Type type, ref SequenceReader<byte> source, BinarySerializationContext context);
}
