using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using StreamRpc.Utils;

namespace StreamRpc.Cil;

internal sealed record FormattedSymbol(string Text);

internal static class CilFormatter
{
    public static string VisualizeIl(MethodBase method)
    {
        var body = method.GetMethodBody();

        var bodyBytes = body?.GetILAsByteArray();
        if (bodyBytes is null)
        {
            return "method has no body";
        }

        var sb = new StringBuilder();
        var isb = new IndentedStringBuilder(sb);
        isb.AppendLine("{");
        isb.PushIndent();

        WriteBodyProlog(body!, isb);
        WriteInstructions(bodyBytes, body!, method.Module, isb);

        isb.PopIndent();
        isb.AppendLine("}");

        return sb.ToString();
    }

    private static FormattedSymbol FormatSymbol(Module reader, EntityHandle handle)
    {
        return new FormattedSymbol(reader.ResolveMember(MetadataTokens.GetToken(handle))?.Name ?? "<null>");
    }

    private static void WriteInstructions(ReadOnlySpan<byte> span, MethodBody body, Module module, IndentedStringBuilder sb)
    {
        var originalLength = span.Length;

        var instructions = new List<CilInstruction>();
        instructions.EnsureCapacity(10);

        HashSet<int>? jumpTargets = null;

        while (!span.IsEmpty)
        {
            var instruction = InstructionDecoder.Read(span,
                                                      originalLength - span.Length,
                                                      t => FormatSymbol(module, t),
                                                      x => module.ResolveString(MetadataTokens.GetToken(x)),
                                                      out int advance);
            span = span[advance..];

            if (InstructionDecoder.IsBranch(instruction.OpCode))
            {
                jumpTargets ??= new();
                jumpTargets.Add(((IConvertible)instruction.Operand!).ToInt32(null));
            }

            instructions.Add(instruction);
        }

        foreach (var (opCode, offset, operand) in instructions)
        {
            foreach (var region in body.ExceptionHandlingClauses)
            {
                if (region.TryOffset == offset)
                {
                    sb.AppendLine(".try {");
                    sb.PushIndent();
                }
                else if (region.HandlerOffset == offset)
                {
                    switch (region.Flags)
                    {
                        case ExceptionHandlingClauseOptions.Clause:
                            break;
                        case ExceptionHandlingClauseOptions.Filter:
                            break;
                        case ExceptionHandlingClauseOptions.Finally:
                            sb.AppendLine("finally {");
                            sb.PushIndent();
                            break;
                        case ExceptionHandlingClauseOptions.Fault:
                            break;
                    }
                }
            }

            if (jumpTargets is { } && jumpTargets.Contains(offset))
            {
                using (sb.WithDedent())
                {
                    sb.Append("IL_");
                    sb.Append(offset.ToString("X4"));
                    sb.AppendLine(":");
                }
            }

            sb.Append(InstructionDecoder.GetText(opCode));

            switch (operand)
            {
                case FormattedSymbol symbol:
                    sb.Append(' ');
                    sb.Append(symbol.Text);
                    break;
                case string strOp:
                    sb.Append(' ');
                    sb.Append('"');
                    sb.Append(strOp);
                    sb.Append('"');
                    break;
                case { } when InstructionDecoder.IsBranch(opCode):
                    sb.Append(" IL_");
                    sb.AppendLine(((IFormattable)operand).ToString("X4", null));
                    break;
                case { }:
                    sb.Append(' ');
                    sb.Append(operand);
                    break;
                case null:
                    break;
            }

            sb.AppendLine();

            var opCodeEndOffset = offset + InstructionDecoder.GetTotalOpCodeSize(opCode);

            foreach (var region in body.ExceptionHandlingClauses)
            {
                if (region.TryOffset + region.TryLength == opCodeEndOffset
                || region.HandlerOffset + region.HandlerLength == opCodeEndOffset)
                {
                    sb.PopIndent();
                    sb.AppendLine("}");
                }
            }
        }
    }

    private static void WriteBodyProlog(MethodBody body, IndentedStringBuilder sb)
    {
        sb.Append(".maxstack ");
        sb.Append(body.MaxStackSize);
        sb.AppendLine();

        sb.Append(".locals ");
        if (body.InitLocals)
        {
            sb.Append("init ");
        }

        sb.Append('(');

        var locals = body.LocalVariables.Select(x =>
        {
            return (x.IsPinned ? "Pinned " : "") +  x.LocalType.Name;
        });
        sb.Append(string.Join(", ", locals));

        sb.Append(')');
        sb.AppendLine();
        sb.AppendLine();
    }
}
