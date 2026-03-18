using System.Diagnostics;

namespace Zarn.Protocol;

internal sealed class CalleeOperations(int maxConcurrentOperations)
{
    private readonly CalleeBase?[] _callees = new CalleeBase?[maxConcurrentOperations];
    private readonly Lock _lock = new();

    public bool Register(CalleeBase callee)
    {
        lock (_lock)
        {
            var freeSlot = Array.IndexOf(_callees, null);
            if (freeSlot < 0)
            {
                return false;
            }

            _callees[freeSlot] = callee;
            return true;
        }
    }

    public CalleeBase? Find(OperationId id)
    {
        lock (_lock)
        {
            foreach (var c in _callees)
            {
                if (c is { } && c.OperationId == id)
                {
                    return c;
                }
            }
            return null;
        }
    }

    public void Remove(CalleeBase callee)
    {
        lock (_lock)
        {
            var slot = Array.IndexOf(_callees, callee);
            Debug.Assert(slot >= 0);
            _callees[slot] = null;
        }
    }
}
