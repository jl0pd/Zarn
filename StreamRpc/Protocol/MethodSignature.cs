using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using StreamRpc.Serialization;

namespace StreamRpc.Protocol;

// Note: signature ignores modreq which is not ideal, but neither C# nor F# allow attaching arbitrary modreq/modopt
// to methods, so we won't have ambiguity unless people start write in MSIL, which is unlikely.
// ECMA 335: II.15.4.0
internal sealed record MethodSignature(string Name, SignatureType[] Parameters, SignatureType ReturnType)
    : IBinarySerializable<MethodSignature>
{
    public static MethodSignature FromMethod(MethodInfo method)
    {
        var ret = SignatureType.Create(method.ReturnType);
        var parameters = method.GetParameters();
        if (parameters.Length == 0)
        {
            return new MethodSignature(method.Name, [], ret);
        }
        else
        {
            var @params = parameters.Select(x => SignatureType.Create(x.ParameterType)).ToArray();
            return new MethodSignature(method.Name, @params, ret);
        }
    }

    public bool Matches(MethodInfo method)
    {
        if (method.Name != Name)
        {
            return false;
        }

        if (ReturnType != SignatureType.Create(method.ReturnType))
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
            if (myParameters[i] != SignatureType.Create(parameters[i].ParameterType))
            {
                return false;
            }
        }

        return true;
    }

    [ExcludeFromCodeCoverage]
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
            sb.AppendJoin(" -> ", Parameters.AsEnumerable());
        }

        sb.Append(" -> ");
        sb.Append(ReturnType);

        return sb.ToString();
    }

    public static MethodSignature Deserialize(ref SequenceReader<byte> source, BinarySerializationContext context)
    {
        var name = context.Deserialize<string>(ref source);
        var parameters = context.Deserialize<SignatureType[]>(ref source);
        var retType = context.Deserialize<SignatureType>(ref source);

        return new MethodSignature(name, parameters, retType);
    }

    public void Serialize(IBufferWriter<byte> writer, BinarySerializationContext context)
    {
        context.Serialize(Name, writer);
        context.Serialize(Parameters, writer);
        context.Serialize(ReturnType, writer);
    }
}
