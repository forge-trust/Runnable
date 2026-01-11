using ForgeTrust.Runnable.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Web.RazorWire;

public class RazorWireWebModule : IRunnableWebModule
{
    public void ConfigureServices(StartupContext context, IServiceCollection services)
    {
        services.AddRazorWire();
        services.AddOutputCache();
        
        services.AddOptions<OutputCacheOptions>()
            .PostConfigure<RazorWireOptions>((options, rwOptions) => 
            {
                options.AddRazorWirePolicies(rwOptions);
            });
    }

    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
        // RazorWire depends on the core web functionality
    }

    public void ConfigureHostBeforeServices(StartupContext context, Microsoft.Extensions.Hosting.IHostBuilder builder)
    {
    }

    public void ConfigureHostAfterServices(StartupContext context, Microsoft.Extensions.Hosting.IHostBuilder builder)
    {
    }

    public void ConfigureWebApplication(StartupContext context, IApplicationBuilder app)
    {
        app.UseOutputCache();
    }

    public void ConfigureEndpoints(StartupContext context, IEndpointRouteBuilder endpoints)
    {
        endpoints.MapRazorWire();
    }
}
