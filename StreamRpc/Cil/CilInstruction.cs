using System.Reflection.Metadata;

namespace StreamRpc.Cil;

internal readonly record struct CilInstruction(ILOpCode OpCode, int Offset, object? Operand);
