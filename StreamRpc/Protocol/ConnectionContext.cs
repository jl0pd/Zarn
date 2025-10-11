using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using StreamRpc.Serialization;

namespace StreamRpc.Protocol;

internal sealed class ConnectionContext
{
    private readonly record struct OutputMessage(
                                        MessageOptions Options,
                                        int Length,
                                        ChunkedArrayPoolBufferWriter<byte> Header,
                                        ChunkedArrayPoolBufferWriter<byte>? Body);

    private readonly Channel<OutputMessage> _outputChannel = Channel.CreateUnbounded<OutputMessage>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false,
    });

    private readonly Stream _stream;
    private readonly MessageOptions _commonOptions;
    private readonly IServiceProvider _calleeServices;
    private readonly ConcurrentDictionary<OperationId, InvokerOperation> _invokerOperations = new();
    private readonly ConcurrentDictionary<OperationId, CalleeBase> _calleeOperations = new();

    public Pools Pools { get; }

    public BinarySerializationContext SerializationContext { get; }

    public ConnectionContext(Stream stream, Pools pools, IServiceProvider services)
    {
        _stream = stream;
        _calleeServices = services;
        Pools = pools;
        SerializationContext = pools.SerializationContext; // keep it closer
    }

    public InvokerBase GetInvoker(Type InvokerType)
    {
        int typeSlot = -1;
        Type interfaceType = InvokerType.IsConstructedGenericType
                                ? InvokerType.GetGenericTypeDefinition()
                                : InvokerType;

        var factories = Pools.InvokerFactories;
        for (int i = 0; i < factories.Length; i++)
        {
            if (factories[i].InterfaceType == interfaceType)
            {
                typeSlot = i;
                break;
            }
        }

        if (typeSlot < 0)
        {
            throw new KeyNotFoundException("Unable to find implementation for type" + InvokerType);
        }

        InvokerBase Invoker;
        if (InvokerType.IsConstructedGenericType)
        {
            Invoker = factories[typeSlot].GetInvoker(InvokerType.GetGenericArguments());
        }
        else
        {
            Invoker = factories[typeSlot].GetInvoker();
        }
        Invoker.Connection = this;
        Invoker.TypeSlot = typeSlot + 1;
        Invoker.Id = OperationId.GenObjectId();

        return Invoker;
    }

    public async Task SendMessages(CancellationToken cancellationToken)
    {
        await foreach (var msg in _outputChannel.Reader.ReadAllAsync(cancellationToken).WithCancellation(cancellationToken))
        {
            if (_commonOptions.HasFlag(MessageOptions.Compressed))
            {
                throw new NotImplementedException();
            }
            await StreamHelper.Send(_stream,
                                    msg.Length,
                                    msg.Options | _commonOptions | MessageOptions.LastChunk,
                                    msg.Header,
                                    msg.Body,
                                    cancellationToken);

            Pools.Return(msg.Header);
            Pools.Return(msg.Body);
        }
    }

    public async Task ReadMessages(CancellationToken cancellationToken)
    {
        var initialBuffer = new byte[PackedInt.MaxSize];
        while (!cancellationToken.IsCancellationRequested)
        {
            var writer = Pools.GetWriter();
            if (!await StreamHelper.Read(_stream, initialBuffer, writer, cancellationToken))
            {
                Pools.Return(writer);
                return;
            }
            HandleMessage(writer);
        }
    }

    private void HandleMessage(ChunkedArrayPoolBufferWriter<byte> message)
    {
        Debug.Assert(((MessageOptions)message.FirstChunk.Array[0] & MessageOptions.ReservedMask) == 0);
        var type = (MessageType)message.FirstChunk.Array[1];
        switch (type)
        {
            case MessageType.ExecuteRequest:
                HandleExecuteRequest(message);
                break;
            case MessageType.ExecuteResponse:
                HandleExecuteResponse(message);
                break;
            case MessageType.ExecuteCancel:
                HandleExecuteCancel(message);
                break;
            default:
                Debug.Fail("Invalid message");
                break;
        }
    }

    private void HandleExecuteCancel(ChunkedArrayPoolBufferWriter<byte> message)
    {
        var reader = message.GetReader();
        reader.Advance(2);

        var opId = SerializationContext.Deserialize<OperationId>(ref reader);

        // it's possible for cancellation to arrive after operation is already completed
        if (_calleeOperations.TryGetValue(opId, out var op))
        {
            Interlocked.Exchange(ref op.Cts, null)?.Cancel();
        }
    }

    private void HandleExecuteRequest(ChunkedArrayPoolBufferWriter<byte> message)
    {
        var reader = message.GetReader();
        var options = (MessageOptions)reader.FirstSpan[0];

        reader.Advance(2);

        var operationId = SerializationContext.Deserialize<OperationId>(ref reader);
        var typeSlot = SerializationContext.Deserialize<int>(ref reader) - 1;
        Type calleeType;
        CalleeBase callee;
        if (options.HasFlag(MessageOptions.GenericType))
        {
            int argsCount = SerializationContext.Deserialize<int>(ref reader);
            var typeArgs = new Type[argsCount];
            for (int i = 0; i < typeArgs.Length; i++)
            {
                typeArgs[i] = SerializationContext.Deserialize<Type>(ref reader);
            }

            calleeType = Pools.CalleeFactories[typeSlot].ImplementationType.MakeGenericType(typeArgs);
            callee = (CalleeBase)Activator.CreateInstance(calleeType)!;
        }
        else
        {
            calleeType = Pools.CalleeFactories[typeSlot].InterfaceType;
            callee = Pools.CalleeFactories[typeSlot].GetCallee();
        }

        callee.MethodSlot = SerializationContext.Deserialize<int>(ref reader);
        if (options.HasFlag(MessageOptions.GenericMethod))
        {
            int argsCount = SerializationContext.Deserialize<int>(ref reader);
            var typeArgs = new Type[argsCount];
            for (int i = 0; i < typeArgs.Length; i++)
            {
                typeArgs[i] = SerializationContext.Deserialize<Type>(ref reader);
            }

            callee.GenericMethodArgs = typeArgs;
        }

        callee.Connection = this;
        callee.OperationId = operationId;
        callee.Arguments = message;
        callee.Impl = _calleeServices.GetService(calleeType) ?? throw new InvalidOperationException();
        callee.ArgumentsReader = reader;
        callee.Cts = Pools.GetCts();

        ThreadPool.UnsafeQueueUserWorkItem(callee, false);
    }

    private void HandleExecuteResponse(ChunkedArrayPoolBufferWriter<byte> message)
    {
        var reader = message.GetReader();
        reader.Advance(2);

        var opId = SerializationContext.Deserialize<OperationId>(ref reader);

        var op = _invokerOperations.Remove(opId);

        var options = (MessageOptions)message.FirstChunk.Array[0];
        if (options.HasFlag(MessageOptions.Success))
        {
            op.Complete(SerializationContext, ref reader);
        }
        else
        {
            var ex = SerializationContext.Deserialize<Exception>(ref reader);
            op.Complete(ex);
        }
    }

    public InvokerOperation<T> StartInvokerOperation<T>(OperationId operationId)
    {
        var operation = Pools.GetInvokerOperation<T>();
        operation.Id = operationId;
        RegisterInvokerOperation(operation);
        return operation;
    }

    public VoidInvokerOperation StartVoidInvokerOperation(OperationId operationId)
    {
        var operation = Pools.GetInvokerOperation();
        operation.Id = operationId;
        RegisterInvokerOperation(operation);
        return operation;
    }

    private void RegisterInvokerOperation(InvokerOperation operation)
    {
        _invokerOperations.AddOrUpdate(operation.Id, operation, (k, v) =>
        {
            string message = "Operation with same Id cannot be registered";
            Debug.Fail(message);
            throw new InvalidOperationException(message);
        });
    }

    public void Dispatch(MessageOptions options, ChunkedArrayPoolBufferWriter<byte> header, ChunkedArrayPoolBufferWriter<byte>? body)
    {
        long length = header.TotalLength;
        if (body is { })
        {
            length += body.TotalLength;
        }

        bool success = _outputChannel.Writer.TryWrite(new OutputMessage(options, checked((int)length), header, body));
        Debug.Assert(success, "Write to unbounded channel should always succeed");
    }

    public void CompleteResponse(CalleeBase callee, Exception? exception, ChunkedArrayPoolBufferWriter<byte>? returnValue)
    {
        var options = exception is null ? MessageOptions.Success : MessageOptions.None;
        var header = Pools.GetWriter();

        header.Reserve(PackedInt.MaxSize);
        SerializationContext.Serialize(options, header);
        SerializationContext.Serialize(MessageType.ExecuteResponse, header);
        SerializationContext.Serialize(callee.OperationId, header);
        if (exception is not null)
        {
            SerializationContext.Serialize(exception, header);
        }

        Dispatch(options, header, returnValue);

        Pools.Return(Interlocked.Exchange(ref callee.Cts, null));

        var @interface = callee.ImplementedInterface;
        if (!@interface.IsGenericType)
        {
            Pools.CalleeFactoryLookup[@interface].Return(callee);
        }
    }
}
