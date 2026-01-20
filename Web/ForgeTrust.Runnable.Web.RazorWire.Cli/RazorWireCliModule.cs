using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

public class RazorWireCliModule : IRunnableHostModule
{
    public void ConfigureServices(StartupContext context, IServiceCollection services)
    {
        services.AddTransient<ExportEngine>();

        // Configure logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
    }

    public void ConfigureHostBeforeServices(StartupContext context, IHostBuilder builder)
    {
    }

    public void ConfigureHostAfterServices(StartupContext context, IHostBuilder builder)
    {
    }

    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
    }
}
