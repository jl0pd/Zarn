using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Zarn;
using Zarn.AspNetCore;

namespace Benchmarks.Services;

[ApiController]
public sealed class ZarnController(ILogger<ZarnController> logger) : ControllerBase
{
    [HttpPost("Zarn/calculator")]
    public async Task RunCalculatorAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Request accepted");

        var sw = Stopwatch.StartNew();

        var server = new RpcServer(AspNetRpcStreamProvider.FromHttpContext(HttpContext));
        server.ConfigureServices(s =>
        {
            s.AllowRemoteConnection<ICalculator>();
            s.AddSingleton<ICalculator, Calculator>();
        });

        var client = await server.AcceptSingleClient(cancellationToken);

        logger.LogInformation("Client connected, delay {Delay}", sw.Elapsed);

        sw.Restart();
        await client.CommunicationEnd;
        await Response.CompleteAsync();

        logger.LogInformation("Communication took {Delay}", sw.Elapsed);
    }

    [HttpPost("Zarn/calculator2")]
    public Task RunCalculatorAsync() => HttpContext.RunRpc(s =>
    {
        s.AllowRemoteConnection<ICalculator>();
        s.AddSingleton<ICalculator, Calculator>();
    });

    [HttpPost("echo")]
    public async Task EchoAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Echo");

        await HttpContext.EnableStreaming(cancellationToken);

        var request = Request.Body;
        var response = Response.Body;
        var buffer = new byte[128];

        while (true)
        {
            int read = await request.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            logger.LogInformation("Echoing {Bytes}", Convert.ToHexString(buffer.AsSpan(0, read)));

            await response.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        logger.LogInformation("End echo");
    }
}
