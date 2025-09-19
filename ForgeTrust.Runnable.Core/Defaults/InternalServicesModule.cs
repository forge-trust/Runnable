using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Core.Defaults;

/// <summary>
/// Internal Services Registration
/// </summary>
internal class InternalServicesModule : IRunnableModule
{
    public void ConfigureServices(StartupContext context, IServiceCollection services)
    {
        services.AddSingleton<IEnvironmentProvider, DefaultEnvironmentProvider>();
    }

    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
    }
}
