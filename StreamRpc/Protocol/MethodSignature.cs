using System.Buffers;
using System.Reflection;
using System.Text;
using StreamRpc.Serialization;

namespace StreamRpc.Protocol;

// Note: signature ignores modreq which is not ideal, but neither C# nor F# allow attaching arbitrary modreq/modopt
// to methods, so we won't have ambiguity unless people start write in MSIL, which is unlikely.
// ECMA 335: II.15.4.0
[BinarySerializer<MethodSignatureBinarySerializer>]
internal sealed record MethodSignature(string Name, SignatureType[] Parameters, SignatureType ReturnType)
{
    public static MethodSignature FromMethod(MethodInfo method)
    {
        var ret = new SignatureType(method.ReturnType);
        var parameters = method.GetParameters();
        if (parameters.Length == 0)
        {
            return new MethodSignature(method.Name, [], ret);
        }
        else
        {
            var @params = parameters.Select(x => new SignatureType(x.ParameterType)).ToArray();
            return new MethodSignature(method.Name, @params, ret);
        }
    }

    public bool Matches(MethodInfo method)
    {
        if (method.Name != Name)
        {
            return false;
        }

        if (ReturnType != new SignatureType(method.ReturnType))
        {
            return false;
        }

        var parameters = method.GetParameters();
        var myParameters = Parameters; // copy to local variable, to avoid bounds checks later in loop
        if (myParameters.Length != parameters.Length)
        {
            return false;
        }

        for (int i = 0; i < parameters.Length; i++)
        {
            if (myParameters[i] != new SignatureType(parameters[i].ParameterType))
            {
                return false;
            }
        }

        return true;
    }

    public override string ToString()
    {
        var sb = new StringBuilder(Name);

        sb.Append(": ");
        if (Parameters.Length == 0)
        {
            sb.Append("()");
        }
        else
        {
            sb.AppendJoin(" -> ", Parameters);
        }

        sb.Append(" -> ");
        sb.Append(ReturnType);

        return sb.ToString();
    }
}

internal sealed class MethodSignatureBinarySerializer : BinarySerializer<MethodSignature>
{
    public override MethodSignature Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        var name = context.Deserialize<string>(ref source);
        var parameters = context.Deserialize<SignatureType[]>(ref source);
        var retType = context.Deserialize<SignatureType>(ref source);

        return new MethodSignature(name, parameters, retType);
    }

    public override void Serialize(MethodSignature value, IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(value.Name, writer);
        context.Serialize(value.Parameters, writer);
        context.Serialize(value.ReturnType, writer);
    }
}
