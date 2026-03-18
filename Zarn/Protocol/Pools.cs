using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Zarn.Compression;
using Zarn.Protocol.EnumerableSupport;
using Zarn.Serialization;
using Zarn.TypeGeneration;
using Zarn.Utils;

namespace Zarn.Protocol;

internal sealed class Pools
{
    public Pools(BinarySerializationContext serializationContext)
    {
        SerializationContext = serializationContext;

        CalleeFactories = [];
        ReverseCalleeFactories = [];

        InvokerFactories = [];
        ReverseInvokerFactories = [];
    }

    public Pools(Pools pools,
                 InterfaceDescriptor[] calleeDescriptors,
                 InterfaceDescriptor[] invokerDescriptors,
                 CompressionProvider? compressionProvider)
    {
        var allCalleeDescriptors = GetAllInterfaces(calleeDescriptors);
        CalleeFactories = allCalleeDescriptors.Select(x => new CalleeFactory(x)).ToArray();
        ReverseCalleeFactories = allCalleeDescriptors.Select(x => new InvokerFactory(x)).ToArray();

        var allInvokerDescriptors = GetAllInterfaces(invokerDescriptors);
        InvokerFactories = allInvokerDescriptors.Select(x => new InvokerFactory(x)).ToArray();
        ReverseInvokerFactories = allInvokerDescriptors.Select(x => new CalleeFactory(x)).ToArray();

        SerializationContext = pools.SerializationContext;
        _onCompletedCache = pools._onCompletedCache;
        _writerPool = pools._writerPool;
        _ctsPool = pools._ctsPool;

        if (compressionProvider is { })
        {
            CompressionProvider = compressionProvider;
            _compressorPool = new Cache<ICompressor>(Environment.ProcessorCount, compressionProvider.CreateCompressor, x => x.Dispose());
            _decompressorPool = new Cache<IDecompressor>(Environment.ProcessorCount, compressionProvider.CreateDecompressor, x => x.Dispose());
        }
    }

    public CalleeFactory[] CalleeFactories { get; }
    public InvokerFactory[] ReverseCalleeFactories { get; }

    public InvokerFactory[] InvokerFactories { get; }
    public CalleeFactory[] ReverseInvokerFactories { get; }

    public CompressionProvider? CompressionProvider { get; }

    public BinarySerializationContext SerializationContext { get; }

    private readonly ConcurrentDictionary<Type, Cache<OnCompletedWorker>> _onCompletedCache = new();
    private readonly Cache<ChunkedArrayPoolBufferWriter<byte>> _writerPool = new(() => new ChunkedArrayPoolBufferWriter<byte>(4096, 65536));
    private readonly Cache<CancellationTokenSource> _ctsPool = new(() => new(), x => x.Dispose());
    private readonly ConcurrentDictionary<Type, Cache<InvokerOperation>> _invokerOperationsCache = new();
    private readonly Cache<ExecuteRequestDispatcher> _executeRequestDispatcherPool = new(() => new ExecuteRequestDispatcher());
    private readonly Cache<ExecuteResponseDispatcher> _executeResponseDispatcherPool = new(() => new ExecuteResponseDispatcher());
    private readonly Cache<ICompressor>? _compressorPool;
    private readonly Cache<IDecompressor>? _decompressorPool;

    public ICompressor? TryGetCompressor() => _compressorPool?.Get();

    public IDecompressor? TryGetDecompressor() => _decompressorPool?.Get();

    public ExecuteRequestDispatcher GetExecuteRequestDispatcher()
    {
        return _executeRequestDispatcherPool.Get();
    }

    public ExecuteResponseDispatcher GetExecuteResponseDispatcher()
    {
        return _executeResponseDispatcherPool.Get();
    }

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

    public MoveNextInvokerOperation<T> GetMoveNextInvokerOperation<T>()
    {
        return Unsafe.As<MoveNextInvokerOperation<T>>(_invokerOperationsCache
                .GetOrAdd(typeof(MoveNextResult<T>), _ => new Cache<InvokerOperation>(() => new MoveNextInvokerOperation<T>()))
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

    public void Return<T>(MoveNextInvokerOperation<T> operation)
    {
        _invokerOperationsCache[typeof(MoveNextResult<T>)].Return(operation);
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

    public void Return(ExecuteRequestDispatcher dispatcher)
    {
        _executeRequestDispatcherPool.Return(dispatcher);
    }

    public void Return(ExecuteResponseDispatcher dispatcher)
    {
        _executeResponseDispatcherPool.Return(dispatcher);
    }

    public void Return(ICompressor compressor)
    {
        Debug.Assert(_compressorPool is { });
        _compressorPool.Return(compressor);
    }

    public void Return(IDecompressor decompressor)
    {
        Debug.Assert(_decompressorPool is { });
        _decompressorPool.Return(decompressor);
    }

    private static List<InterfaceDescriptor> GetAllInterfaces(InterfaceDescriptor[] descriptors)
    {
        var result = new List<InterfaceDescriptor>();
        var seen = new HashSet<Type>();

        var queue = new Queue<InterfaceDescriptor>(descriptors);

        while (queue.TryDequeue(out var descriptor))
        {
            var type = descriptor.ResolveType();
            if (type is { } && seen.Add(type))
            {
                result.Add(descriptor);
                foreach (var method in descriptor.ResolveMethods())
                {
                    if (method is { })
                    {
                        var parameters = method.GetParameters();
                        foreach (var param in parameters)
                        {
                            if (param.ParameterType.IsInterface)
                            {
                                queue.Enqueue(InterfaceDescriptor.FromType(param.ParameterType));
                            }
                        }
                    }
                }
            }
        }

        return result;
    }
}
