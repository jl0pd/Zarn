using System.Runtime.CompilerServices;

namespace StreamRpc.Utils;

internal static class ThreadingHelper
{
    public static ConfiguredTaskAwaitable LeaveContext()
    {
        return Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
    }

    public static async void Fire(this Task task)
    {
        try
        {
            await task;
        }
        catch
        {
        }
    }
}
