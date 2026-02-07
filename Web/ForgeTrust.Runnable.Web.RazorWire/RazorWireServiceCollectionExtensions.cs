using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ForgeTrust.Runnable.Web.RazorWire.Streams;
using ForgeTrust.Runnable.Web.RazorWire.Bridge;

namespace ForgeTrust.Runnable.Web.RazorWire;

/// <summary>
/// Provides extension methods for registering RazorWire services into the <see cref="IServiceCollection"/>.
/// </summary>
public static class RazorWireServiceCollectionExtensions
{
    /// <summary>
    /// Registers RazorWire options and default RazorWire services into the provided <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection to register RazorWire services into.</param>
    /// <param name="configure">Optional action to configure <see cref="RazorWireOptions"/>; if null, default options are used.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance with RazorWire registrations added.</returns>
    public static IServiceCollection AddRazorWire(
        this IServiceCollection services,
        Action<RazorWireOptions>? configure = null)
    {
        services.AddOptions<RazorWireOptions>();

        services.Configure(configure ?? (_ => { }));

        services.AddSingleton(sp =>
            sp.GetRequiredService<IOptions<RazorWireOptions>>().Value);

        services.TryAddSingleton<IRazorWireStreamHub, InMemoryRazorWireStreamHub>();
        services.TryAddSingleton<IRazorWireChannelAuthorizer, DefaultRazorWireChannelAuthorizer>();
        services.TryAddSingleton<IRazorPartialRenderer, RazorPartialRenderer>();

        return services;
    }
}