using Zarn.Serialization;

namespace Zarn.Protocol.Messages;

internal interface IMessageInternal<T> : IBinarySerializable<T> where T : struct, IBinarySerializable<T>
{
    MessageType Type { get; }
}
