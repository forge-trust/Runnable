using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ForgeTrust.Runnable.Web.Tailwind;

/// <summary>
/// Provides extension methods for registering Tailwind CSS services.
/// </summary>
public static class TailwindExtensions
{
    /// <summary>
    /// Adds Tailwind CSS services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTailwind(this IServiceCollection services)
    {
        services.Configure<TailwindOptions>(_ => { });
        services.AddSingleton<TailwindCliManager>();
        services.AddHostedService<TailwindWatchService>();

        return services;
    }

    /// <summary>
    /// Adds Tailwind CSS services with custom configuration to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">An action to configure the <see cref="TailwindOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTailwind(this IServiceCollection services, Action<TailwindOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<TailwindCliManager>();
        services.AddHostedService<TailwindWatchService>();
        
        return services;
    }
}
