using System.Buffers;

namespace Zarn.Tests.Utils;

public sealed class SequenceHelperTests
{
    static void AssertSeqEqual<T>(ReadOnlySequence<T> seq, T[][] chunks)
    {
        Assert.Equal(chunks.Sum(x => x.Length), seq.Length);

        var it = seq.GetEnumerator();

        foreach (var chunk in chunks)
        {
            Assert.True(it.MoveNext());
            Assert.Equal(chunk, it.Current.ToArray());
        }

        Assert.False(it.MoveNext());
    }

    [Fact]
    public void TestSplit()
    {
        var seq = SequenceHelper.Split([0, 1, 2, 3, 4, 5], 2);

        AssertSeqEqual(seq, [
            [0, 1],
            [2, 3],
            [4, 5],
        ]);
    }

    [Fact]
    public void TestSplit2()
    {
        var seq = SequenceHelper.Split([0, 1, 2, 3, 4, 5, 6, 7], 3);
        AssertSeqEqual(seq, [
            [0, 1, 2],
            [3, 4, 5],
            [6, 7],
        ]);
    }

    [Fact]
    public void TestSplit3()
    {
        var seq = SequenceHelper.Split([0, 1, 2, 3, 4, 5, 6], 3);
        AssertSeqEqual(seq, [
            [0, 1, 2],
            [3, 4, 5],
            [6],
        ]);
    }
}
