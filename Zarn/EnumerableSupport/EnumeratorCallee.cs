using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Zarn.Invocation;

namespace Zarn.EnumerableSupport;

internal sealed class EnumeratorCallee<T> : CalleeBase
{
    private IEnumerator<T>? _asSync;
    private IAsyncEnumerator<T>? _asAsync;
    private ValueTaskAwaiter<bool> _moveNextAwaiter;
    private Action? _onMoveNextCompleted;

    internal override object Impl
    {
        get => (object?)_asAsync ?? _asSync!;
        set
        {
            if (value is IAsyncEnumerator<T> asyncEnum)
            {
                _asAsync = asyncEnum;
            }
            if (value is IEnumerator<T> syncEnum)
            {
                _asSync = syncEnum;
            }

            Debug.Assert(_asSync is { } || _asAsync is { });
        }
    }

    protected internal override void DispatchCore(ref SequenceReader<byte> argumentsReader, int methodSlot)
    {
        Debug.Assert(argumentsReader.End);

        switch ((EnumeratorMethod)(methodSlot + 1))
        {
            case EnumeratorMethod.Dispose:
                InvokeDispose();
                break;
            case EnumeratorMethod.DisposeAsync:
                InvokeDisposeAsync();
                break;
            case EnumeratorMethod.MoveNext:
                InvokeMoveNext();
                break;
            case EnumeratorMethod.MoveNextAsync:
                InvokeMoveNextAsync();
                break;
            case EnumeratorMethod.Reset:
                InvokeReset();
                break;
            default:
                throw ThrowHelper.Fail("Invalid method slot was passed for invoke");
        }
    }

    private void InvokeReset()
    {
        Debug.Assert(_asSync is { });
        try
        {
            _asSync.Reset();
            CompleteVoid();
        }
        catch (Exception e)
        {
            Fail(e);
        }
    }

    private void InvokeMoveNextAsync()
    {
        Debug.Assert(_asAsync is { });
        try
        {
            var moveNextAwaiter = _asAsync.MoveNextAsync().GetAwaiter();
            if (moveNextAwaiter.IsCompleted)
            {
                var moveNextResult = moveNextAwaiter.GetResult();
                if (moveNextResult)
                {
                    Complete(new MoveNextResult<T>(true, _asAsync.Current));
                }
                else
                {
                    Complete(default(MoveNextResult<T>));
                }
            }
            else
            {
                _moveNextAwaiter = moveNextAwaiter;
                _moveNextAwaiter.UnsafeOnCompleted(_onMoveNextCompleted ??= OnMoveNextCompleted);
            }
        }
        catch (Exception e)
        {
            Fail(e);
        }
    }

    private void OnMoveNextCompleted()
    {
        Debug.Assert(_asAsync is { });
        Debug.Assert(_moveNextAwaiter.IsCompleted);

        var awaiter = _moveNextAwaiter;
        _moveNextAwaiter = default; // cleanup references that may be captured

        try
        {
            var moveNextResult = awaiter.GetResult();
            if (moveNextResult)
            {
                Complete(new MoveNextResult<T>(true, _asAsync.Current));
            }
            else
            {
                Complete(default(MoveNextResult<T>));
            }
        }
        catch (Exception e)
        {
            Fail(e);
        }
    }

    private void InvokeMoveNext()
    {
        Debug.Assert(_asSync is { });
        try
        {
            bool success = _asSync.MoveNext();
            if (success)
            {
                Complete(new MoveNextResult<T>(true, _asSync.Current));
            }
            else
            {
                Complete(default(MoveNextResult<T>));
            }
        }
        catch (Exception e)
        {
            Fail(e);
        }
    }

    private void InvokeDisposeAsync()
    {
        Debug.Assert(_asAsync is { });
        try
        {
            WaitVoidTask(_asAsync.DisposeAsync());
        }
        catch (Exception e)
        {
            Fail(e);
        }
    }

    private void InvokeDispose()
    {
        Debug.Assert(_asSync is { });
        try
        {
            _asSync.Dispose();
            CompleteVoid();
        }
        catch (Exception e)
        {
            Fail(e);
        }
    }
}
