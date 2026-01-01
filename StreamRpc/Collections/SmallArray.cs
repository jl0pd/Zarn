using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace StreamRpc.Collections;

internal static class SmallArray
{
    public static SmallArray0<T> Create<T>()
    {
        return default;
    }

    public static SmallArray1<T> Create<T>(T i1)
    {
        return new SmallArray1<T>
        {
            First = i1,
        };
    }

    public static SmallArray2<T> Create<T>(T i1, T i2)
    {
        return new SmallArray2<T>
        {
            First = i1,
            Second = i2,
        };
    }

    public static SmallArray3<T> Create<T>(T i1, T i2, T i3)
    {
        return new SmallArray3<T>
        {
            First = i1,
            Second = i2,
            Third = i3,
        };
    }

    public static SmallArray4<T> Create<T>(T i1, T i2, T i3, T i4)
    {
        return new SmallArray4<T>
        {
            First = i1,
            Second = i2,
            Third = i3,
            Fourth = i4,
        };
    }
}

internal interface ISmallArray<T>
{
    int Length { get; }

    ReadOnlySpan<T> AsSpan();
}

internal readonly struct SmallArray0<T> : ISmallArray<T>
{
    public int Length => 0;

    public ReadOnlySpan<T> AsSpan() => [];
}

internal struct SmallArray1<T> : ISmallArray<T>
{
    public T First { readonly get => _first; set => _first = value; }
    private T _first;

    public readonly int Length => 1;

    public readonly ReadOnlySpan<T> AsSpan() => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _first), 1);
}

[StructLayout(LayoutKind.Sequential)]
internal struct SmallArray2<T> : ISmallArray<T>
{
    public T First { readonly get => _first; set => _first = value; }
    private T _first;

    public T Second { get; set; }

    public readonly int Length => 2;

    public readonly ReadOnlySpan<T> AsSpan() => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _first), 2);
}

[StructLayout(LayoutKind.Sequential)]
internal struct SmallArray3<T> : ISmallArray<T>
{
    public T First { readonly get => _first; set => _first = value; }
    private T _first;

    public T Second { get; set; }
    
    public T Third { get; set; }

    public readonly int Length => 3;

    public readonly ReadOnlySpan<T> AsSpan() => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _first), 3);
}

[StructLayout(LayoutKind.Sequential)]
internal struct SmallArray4<T> : ISmallArray<T>
{
    public T First { readonly get => _first; set => _first = value; }
    private T _first;

    public T Second { get; set; }

    public T Third { get; set; }

    public T Fourth { get; set; }

    public readonly int Length => 4;

    public readonly ReadOnlySpan<T> AsSpan() => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _first), 4);
}
