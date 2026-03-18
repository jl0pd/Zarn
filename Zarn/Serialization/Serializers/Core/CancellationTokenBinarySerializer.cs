using System.Buffers;

namespace Zarn.Serialization.Serializers.Core;

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
