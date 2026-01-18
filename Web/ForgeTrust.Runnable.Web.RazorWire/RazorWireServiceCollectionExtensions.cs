using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ForgeTrust.Runnable.Web.RazorWire.Streams;

namespace ForgeTrust.Runnable.Web.RazorWire;

public static class RazorWireServiceCollectionExtensions
{
    public static IServiceCollection AddRazorWire(
        this IServiceCollection services,
        Action<RazorWireOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }

        services.AddSingleton(sp =>
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RazorWireOptions>>().Value);

        services.TryAddSingleton<IRazorWireStreamHub, InMemoryRazorWireStreamHub>();
        services.TryAddSingleton<IRazorWireChannelAuthorizer, DefaultRazorWireChannelAuthorizer>();

        return services;
    }
}
