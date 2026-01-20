using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ForgeTrust.Runnable.Web.RazorWire.Streams;

namespace ForgeTrust.Runnable.Web.RazorWire;

public static class RazorWireServiceCollectionExtensions
{
    /// <summary>
    /// Registers RazorWire services and configuration into the provided service collection.
    /// </summary>
    /// <param name="services">The service collection to register RazorWire services into.</param>
    /// <param name="configure">Optional action to configure <see cref="RazorWireOptions"/>; when provided, options are applied via the options pattern.</param>
    /// <summary>
    /// Registers RazorWire options and default RazorWire services into the provided service collection.
    /// </summary>
    /// <param name="configure">Optional configuration action for <see cref="RazorWireOptions"/>; if null, no changes are applied.</param>
    /// <summary>
    /// Registers RazorWire options and default RazorWire services into the provided <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="configure">Optional action to configure <see cref="RazorWireOptions"/>; may be null.</param>
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

        return services;
    }
}