using Microsoft.AspNetCore.Http;

namespace StreamRpc.AspNetCore;

public static class AspNetRpcStreamProvider
{
    public static RpcStreamProvider FromHttpContext(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            throw new ArgumentException("Request method must be POST", nameof(context));
        }

        return new FromHttpContextProvider(context);
    }

    private sealed class FromHttpContextProvider(HttpContext context) : RpcStreamProvider
    {
        public override async ValueTask<Stream?> OpenStreamAsync(CancellationToken cancellationToken)
        {
            await context.EnableStreaming(cancellationToken);
            return new AspNetCombinedStream(context.Request.Body, context.Response.Body);
        }
    }
}
