using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Core;

/// <summary>
/// Defines a module that can configure services and register dependencies within the Runnable host.
/// </summary>
public interface IRunnableModule
{
    /// <summary>
    /// Configures the services for this module.
    /// </summary>
    /// <param name="context">The context for the current startup process.</param>
    /// <param name="services">The service collection to add registrations to.</param>
    void ConfigureServices(StartupContext context, IServiceCollection services);

    /// <summary>
    /// Registers other modules that this module depends on.
    /// </summary>
    /// <param name="builder">The builder used to declare module dependencies.</param>
    void RegisterDependentModules(ModuleDependencyBuilder builder);
}
