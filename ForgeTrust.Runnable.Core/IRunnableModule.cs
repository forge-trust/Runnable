using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Core;

public interface IRunnableModule
{
    void ConfigureServices(StartupContext context, IServiceCollection services);

    void RegisterDependentModules(ModuleDependencyBuilder builder);
}
