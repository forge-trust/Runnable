namespace ForgeTrust.Runnable.Core;

using Microsoft.Extensions.DependencyInjection;

public interface IRunnableModule
{
    void ConfigureServices(StartupContext context, IServiceCollection services);
    
    void RegisterDependentModules(ModuleDependencyBuilder builder);
}
