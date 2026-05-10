# Zarn

Zarn stands for **Z**ero **A**llocation **R**pc for dot**N**et.
It's a library for seamless communication with remote process over arbitrary `System.IO.Stream`.

## Features

* **Amortized zero allocations**:
Usage of `ValueTask<>` allows to reuse state between requests, avoiding allocations
that would've been required for `Task<>`. `struct` parameters doesn't need allocations,
so call to following `IAdder` won't need to allocate nothing both on client and on server.

```csharp
public interface IAdder
{
    ValueTask<int> AddAsync(int a, int b);
}
```

* **Threadsafe**: All objects can be used by multiple threads simultaneously
without risk of deadlocking or corruption.

* **Blazing fast**: Has higher throughput, lower latency and no gen0 allocation compared to gRpc

| Method | Mean      | Error    | StdDev   | Gen0   | Allocated |
|------- |----------:|---------:|---------:|-------:|----------:|
| Grpc   | 123.82 μs | 0.846 μs | 0.791 μs | 0.2441 |    6614 B |
| Zarn   |  87.00 μs | 0.517 μs | 0.484 μs |      - |     953 B |

See [Benchmarks folder](./Benchmarks.Client/) for more info

* **`IAsyncEnumerable` support**: Both sync and async enumerables can be returned
from methods and passed as parameters supporting lazy enumeration in both directions.

* **Referential transparency of passed interfaces**: Depending on parameter type
value is either passed by-value or by-proxy across RPC. `struct` and `class`
parameters are serialized as is and deserialized back, making full copy. On the
other hand for `interface` proxy is generated which is tracked by RPC infrastructure.
This can be used to pass statefull object across RPC.

```csharp
interface IWorker { }
interface IStore
{
    ValueTask SaveWorker(IWorker? worker);
    ValueTask<IWorker?> GetWorker();
}

class Store : IStore
{
    private IWorker? _worker;
    ValueTask SaveWorker(IWorker? worker) => _worker = worker;
    ValueTask<IWorker?> GetWorker() => ValueTask.FromResult(_worker);
}

var worker = new Worker();
var store = client.GetRemoteService<IStore>();
await store.SaveWorker(worker);
Assert.Same(worker, await store.GetWorker());
```

* **No schema**: Just write interface, register and it will work. No need to
mess with additional files and generators, your code already denotes schema.

* **Transparent exceptions**: Exception's type and stacktrace is kept when
exception is thrown in other process. This semantics may be configured using
`RpcSettings.UnhandledExceptionPropagationBehavior` and `RpcSettings.TransparentExceptions`

* **`CancellationToken` support**: `CancellationToken`s can be used and when
cancelled they propagate cancellation across RPC.

* **Compression support**: Opt-in compression support for smaller network traffic.
Create `RpcServer` and `RpcClient` passing `RpcSettings` with property `CompressionProviders` initialized.
If both parties support same compression, then it will be used. Providers closer
to start of list has higher priority. Has built-in `BrotliCompressionProvider`.

* **Dependency injection**: Both client and server use familiar infrastructure
from `Microsoft.Extensions.DependencyInjection`.

* **Configurable serialization**: By-default types are serialized using `System.Text.Json`,
so you don't need to worry about complexity of writing own serializer.
Alternative serializer based on `BinarySerializer` can be provided for better
performance. See doc on `RpcSettings.Serializers` for more details.

* **Asp.net support**: Package `Zarn.AspNetCore` provides support for running RPC
server inside Aspnet over http2 connection.

```csharp
// inside startup code configure RpcSettings
builder.Services.AddSingleton(new RpcSettings {});

// add endpoint to controller
[HttpPost("path/to/rpc/endpoint")]
public Task RunRpc() => HttpContext.RunRpc(s =>
{
    // configure RpcServer's services here
});

// connect from client
using var httpClient = new HttpClient();
await using var client = new RpcClient(RpcStreamProvider.FromHttp2Endpoint(
                                            httpClient,
                                            new Uri("https://localhost:9090/path/to/rpc/endpoint")
                                       ));
```

## Quick start

1. Define interface in shared library

    ```csharp
    public interface IGreeter
    {
        ValueTask<string> GetGreetingAsync(string name);
    }
    ```

2. Implement interface on server

    ```csharp
    internal sealed class Greeter : IGreeter
    {
        public async ValueTask<string> GetGreetingAsync(string name)
        {
            return $"Hello, {name}";
        }
    }
    ```

3. Register implementation on server and start it

    ```csharp
    await using var server = new RpcServer(RpcStreamProvider.FromListenPort(IPEndPoint.Parse("127.0.0.1:9090")));

    server.ConfigureServices(s =>
    {
        s.AddScoped<IGreeter, Greeter>();
        s.AllowRemoteConnection<IGreeter>();
    });

    await foreach (var client in server.Start())
    {
        // log connection or invoke methods on client
    }
    ```

4. Connect with client and get implementation

    ```csharp
    await using var client = new RpcClient(RpcStreamProvider.FromServerIp(IPEndPoint.Parse("127.0.0.1:9090")))
    await client.ConnectAsync(cancellationToken);

    var greeter = client.GetRemoteService<IGreeter>();

    Console.WriteLine(await greeter.GetGreeting("World"));
    ```
