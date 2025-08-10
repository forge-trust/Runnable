using ForgeTrust.Runnable.Core;
using ForgeTrust.Runnable.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Routing;

public class ExampleModule : IRunnableWebModule
{
    public void ConfigureServices(StartupContext context, IServiceCollection services)
    {
        // Register services for the application here if needed
    }

    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
        // Add module dependencies here if needed
    }

    public void ConfigureHostBeforeServices(StartupContext context, IHostBuilder builder)
    {
    }

    public void ConfigureHostAfterServices(StartupContext context, IHostBuilder builder)
    {
    }

    public void ConfigureWebApplication(StartupContext context, IApplicationBuilder app)
    {
        if (app is IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/", () => "Hello from web app example!");
        }
    }
}
