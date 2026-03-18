using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zarn.Serialization.Serializers.Core;

internal sealed class JsonBinarySerializerFactory : BinarySerializerFactory
{
    public override bool CanConvert(Type type) => true;

    public override BinarySerializer CreateSerializer(Type type)
    {
        var serType = typeof(JsonBinarySerializer<>).MakeGenericType(type);
        var result = (BinarySerializer)Activator.CreateInstance(serType, [_options])!;
        return result;
    }

    private readonly JsonSerializerOptions _options = new()
    {
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };
}
