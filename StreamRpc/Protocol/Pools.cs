using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using StreamRpc.Serialization;
using StreamRpc.TypeGeneration;
using StreamRpc.Utils;

namespace StreamRpc.Protocol;

internal sealed class Pools
{
    public Pools(BinarySerializationContext serializationContext)
    {
        SerializationContext = serializationContext;
        CalleeFactories = [];
        InvokerFactories = [];
    }

    public Pools(Pools pools, InterfaceDescriptor[] calleeDescriptors, InterfaceDescriptor[] InvokerDescriptors)
    {
        CalleeFactories = calleeDescriptors.Select(x => new CalleeFactory(x)).ToArray();

        InvokerFactories = InvokerDescriptors.Select(x => new InvokerFactory(x)).ToArray();

        SerializationContext = pools.SerializationContext;
        _onCompletedCache = pools._onCompletedCache;
        _writerPool = pools._writerPool;
        _ctsPool = pools._ctsPool;
    }

    public CalleeFactory[] CalleeFactories { get; }
    public InvokerFactory[] InvokerFactories { get; }

    public BinarySerializationContext SerializationContext { get; }

    private readonly ConcurrentDictionary<Type, Cache<OnCompletedWorker>> _onCompletedCache = new();
    private readonly Cache<ChunkedArrayPoolBufferWriter<byte>> _writerPool = new(() => new ChunkedArrayPoolBufferWriter<byte>(4096, 65536));
    private readonly Cache<CancellationTokenSource> _ctsPool = new(() => new(), x => x.Dispose());
    private readonly ConcurrentDictionary<Type, Cache<InvokerOperation>> _invokerOperationsCache = new();

    public VoidOnCompletedWorker GetOnCompletedWorker()
    {
        return Unsafe.As<VoidOnCompletedWorker>(_onCompletedCache
                .GetOrAdd(typeof(void), _ => new Cache<OnCompletedWorker>(() => new VoidOnCompletedWorker()))
                .Get());
    }

    public OnCompletedWorker<T> GetOnCompletedWorker<T>()
    {
        return Unsafe.As<OnCompletedWorker<T>>(_onCompletedCache
                .GetOrAdd(typeof(T), _ => new Cache<OnCompletedWorker>(() => new OnCompletedWorker<T>()))
                .Get());
    }

    public VoidInvokerOperation GetInvokerOperation()
    {
        return Unsafe.As<VoidInvokerOperation>(_invokerOperationsCache
                .GetOrAdd(typeof(void), _ => new Cache<InvokerOperation>(() => new VoidInvokerOperation()))
                .Get());
    }

    public InvokerOperation<T> GetInvokerOperation<T>()
    {
        return Unsafe.As<InvokerOperation<T>>(_invokerOperationsCache
                .GetOrAdd(typeof(T), _ => new Cache<InvokerOperation>(() => new InvokerOperation<T>()))
                .Get());
    }

    public ChunkedArrayPoolBufferWriter<byte> GetWriter()
    {
        return _writerPool.Get();
    }

    public CancellationTokenSource GetCts()
    {
        return _ctsPool.Get();
    }

    public void Return(VoidOnCompletedWorker worker)
    {
        _onCompletedCache[typeof(void)].Return(worker);
    }

    public void Return<T>(OnCompletedWorker<T> worker)
    {
        _onCompletedCache[typeof(T)].Return(worker);
    }

    public void Return(VoidInvokerOperation operation)
    {
        _invokerOperationsCache[typeof(void)].Return(operation);
    }

    public void Return<T>(InvokerOperation<T> operation)
    {
        _invokerOperationsCache[typeof(T)].Return(operation);
    }

    public void Return(ChunkedArrayPoolBufferWriter<byte>? writer)
    {
        if (writer is null)
        {
            return;
        }

        writer.Reset();
        _writerPool.Return(writer);
    }

    public void Return(CancellationTokenSource? cts)
    {
        if (cts is { } && cts.TryReset())
        {
            _ctsPool.Return(cts);
        }
    }
}
