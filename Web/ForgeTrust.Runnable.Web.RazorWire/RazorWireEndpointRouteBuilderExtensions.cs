using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ForgeTrust.Runnable.Web.RazorWire.Streams;

namespace ForgeTrust.Runnable.Web.RazorWire;

public static class RazorWireEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapRazorWire(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<RazorWireOptions>();

        endpoints.MapGet($"{options.Streams.BasePath}/{{channel}}", async context =>
        {
            var channel = context.Request.RouteValues["channel"] as string;
            if (string.IsNullOrEmpty(channel))
            {
                context.Response.StatusCode = 400;
                return;
            }

            var authorizer = context.RequestServices.GetRequiredService<IRazorWireChannelAuthorizer>();
            if (!await authorizer.CanSubscribeAsync(context, channel))
            {
                context.Response.StatusCode = 403;
                return;
            }

            var hub = context.RequestServices.GetRequiredService<IRazorWireStreamHub>();
            
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers.Connection = "keep-alive";

            var reader = hub.Subscribe(channel);

            try
            {
                await foreach (var message in reader.ReadAllAsync(context.RequestAborted))
                {
                    await context.Response.WriteAsync($"data: {message}\n\n", context.RequestAborted);
                    await context.Response.Body.FlushAsync(context.RequestAborted);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal exit
            }

        }).ExcludeFromDescription();

        return endpoints;
    }
}
