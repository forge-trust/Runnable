namespace ForgeTrust.Runnable.Core;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public interface IRunnableHostModule : IRunnableModule
{
    void ConfigureHostBeforeServices(StartupContext context, IHostBuilder builder);
    
    void ConfigureHostAfterServices(StartupContext context, IHostBuilder builder);
    
}
