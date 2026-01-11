using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ForgeTrust.Runnable.Web.RazorWire.Streams;

namespace ForgeTrust.Runnable.Web.RazorWire;

public static class RazorWireServiceCollectionExtensions
{
    public static IServiceCollection AddRazorWire(this IServiceCollection services, Action<RazorWireOptions>? configure = null)
    {
        var options = new RazorWireOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        services.TryAddSingleton<IRazorWireStreamHub, InMemoryRazorWireStreamHub>();
        services.TryAddSingleton<IRazorWireChannelAuthorizer, DefaultRazorWireChannelAuthorizer>();

        return services;
    }
}
