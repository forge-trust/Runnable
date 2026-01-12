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
            context.Response.Headers.Pragma = "no-cache";

            var reader = hub.Subscribe(channel);

            try
            {
                // 1. Send initial comment to establish connection and flush headers
                await context.Response.WriteAsync(":\n\n", context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);

                // 2. Loop with heartbeat support
                while (!context.RequestAborted.IsCancellationRequested)
                {
                    var readTask = reader.ReadAsync(context.RequestAborted).AsTask();
                    var heartbeatTask = Task.Delay(20000, context.RequestAborted); // 20s heartbeat

                    var completedTask = await Task.WhenAny(readTask, heartbeatTask);

                    if (completedTask == readTask)
                    {
                        var message = await readTask;
                        await context.Response.WriteAsync($"data: {message}\n\n", context.RequestAborted);
                        await context.Response.Body.FlushAsync(context.RequestAborted);
                    }
                    else
                    {
                        // Send heartbeat comment
                        await context.Response.WriteAsync(":\n\n", context.RequestAborted);
                        await context.Response.Body.FlushAsync(context.RequestAborted);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal exit on client disconnect
            }
            finally
            {
                hub.Unsubscribe(channel, reader);
            }

        }).ExcludeFromDescription();

        return endpoints;
    }
}
