using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace StreamRpc;

internal static class ThrowHelper
{
    public static Exception Unreachable
    {
        get
        {
            var message = "Execution has reached point that is thought to be unreachable";
            Debug.Fail(message);
            return new Exception(message);
        }
    }

    [DoesNotReturn]
    public static Exception Fail(string message)
    {
        Debug.Fail(message);
        return new Exception(message);
    }
}
