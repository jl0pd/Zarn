using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace StreamRpc;

internal static class ThrowHelper
{
    [ExcludeFromCodeCoverage]
    public static Exception Unreachable
    {
        get
        {
            var message = "Execution has reached point that is thought to be unreachable";
            Debug.Fail(message);
            return new UnreachableException(message);
        }
    }

    [DoesNotReturn]
    public static void ThrowEndOfStream()
    {
        throw new EndOfStreamException();
    }

    [DoesNotReturn]
    [ExcludeFromCodeCoverage]
    public static Exception Fail(string message)
    {
        Debug.Fail(message);
        return new Exception(message);
    }
}
