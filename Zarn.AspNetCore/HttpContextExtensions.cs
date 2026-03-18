using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Zarn.AspNetCore;

public static class HttpContextExtensions
{
    public static Task EnableStreaming(this HttpContext context, CancellationToken cancellationToken)
    {
        // copied from https://github.com/davidfowl/StreamingSample/blob/020357917831f1e74432277b0a95be4e11050ddb/server/Startup.cs#L21

        // We're streaming here so there's no max body size nor is there a min data rate
        context.Features.Get<IHttpMaxRequestBodySizeFeature>()!.MaxRequestBodySize = null;
        context.Features.Get<IHttpMinRequestBodyDataRateFeature>()!.MinDataRate = null;

        // Flush the headers so that the client can start sending
        return context.Response.Body.FlushAsync(cancellationToken);
    }

    public static async Task RunRpc(this HttpContext context, Action<IServiceCollection> configureServices)
    {
        var streamProvider = AspNetRpcStreamProvider.FromHttpContext(context);
        var settings = context.RequestServices.GetService<RpcSettings>();
        var server = new RpcServer(streamProvider, settings);
        server.ConfigureServices(configureServices);

        var client = await server.AcceptSingleClient(context.RequestAborted);
        await client.CommunicationEnd;
    }
}
