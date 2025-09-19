using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ForgeTrust.Runnable.Core.Defaults;

/// <summary>
/// A basic implementation of <see cref="IRunnableHostModule"/> that does nothing.
/// This is useful for providing implementations of apps that do not require a module.
///
/// Primarily used
/// </summary>
public class NoHostModule : IRunnableHostModule
{
    public void ConfigureServices(StartupContext context, IServiceCollection services)
    {

    }

    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {

    }

    public void ConfigureHostBeforeServices(StartupContext context, IHostBuilder builder)
    {

    }

    public void ConfigureHostAfterServices(StartupContext context, IHostBuilder builder)
    {

    }
}
