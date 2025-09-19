using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Core.Defaults;

/// <summary>
/// Internal Services Registration
/// </summary>
internal class InternalServicesModule : IRunnableModule
{
    public void ConfigureServices(StartupContext context, IServiceCollection services)
    {
        services.AddSingleton(context.EnvironmentProvider);
    }

    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
    }
}
