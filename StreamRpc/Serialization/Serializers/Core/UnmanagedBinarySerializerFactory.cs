using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace StreamRpc.Serialization.Serializers.Core;

internal sealed class UnmanagedBinarySerializerFactory : BinarySerializerFactory
{
    public static UnmanagedBinarySerializerFactory Instance { get; } = new();

    private static readonly MethodInfo IsReferenceOrContainsReference
        = typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.IsReferenceOrContainsReferences))!;

    public override bool CanConvert(Type type)
    {
        return !(bool)IsReferenceOrContainsReference.MakeGenericMethod(type).Invoke(null, null)!;
    }

    public override BinarySerializer CreateSerializer(Type type)
    {
        return (BinarySerializer)Activator.CreateInstance(typeof(Serializer<>).MakeGenericType(type))!;
    }

    private sealed class Serializer<T> : BinarySerializer<T> where T : unmanaged
    {
        public override T Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
        {
            Span<byte> value = stackalloc byte[Unsafe.SizeOf<T>()];
            source.UnreadSequence.Slice(0, Unsafe.SizeOf<T>()).CopyTo(value);
            source.Advance(Unsafe.SizeOf<T>());
            return MemoryMarshal.Read<T>(value);
        }

        public override void Serialize(T value, IBufferWriter<byte> writer, BinarySerializationContext context)
        {
            var span = writer.GetSpan(Unsafe.SizeOf<T>());
            MemoryMarshal.Write(span, in value);
            writer.Advance(Unsafe.SizeOf<T>());
        }
    }
}
