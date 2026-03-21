using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ForgeTrust.Runnable.Web.Tailwind;

/// <summary>
/// Provides extension methods for registering Tailwind CSS services.
/// </summary>
public static class TailwindExtensions
{
    /// <summary>
    /// Adds Tailwind CSS services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">An optional action to configure <see cref="TailwindOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTailwind(this IServiceCollection services, Action<TailwindOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<TailwindCliManager>();
        services.AddHostedService<TailwindWatchService>();
        
        return services;
    }
}
