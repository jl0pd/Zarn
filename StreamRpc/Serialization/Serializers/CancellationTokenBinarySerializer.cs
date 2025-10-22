using System.Buffers;

namespace StreamRpc.Serialization.Serializers;

internal sealed class CancellationTokenBinarySerializer : BinarySerializer<CancellationToken>
{
    public static CancellationTokenBinarySerializer Instance { get; } = new();

    public override CancellationToken Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        return new CancellationToken(context.Deserialize<bool>(ref source));
    }

    public override void Serialize(CancellationToken value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(value.IsCancellationRequested, writer);
    }
}
