using System.Collections.Concurrent;

namespace StreamRpc;

internal static class Extensions
{
    public static TValue Remove<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> dict, TKey key) where TKey : notnull
    {
        if (dict.TryRemove(key, out var value))
        {
            return value;
        }
        else
        {
            throw new KeyNotFoundException();
        }
    }
}
