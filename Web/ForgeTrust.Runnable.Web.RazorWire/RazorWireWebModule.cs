using ForgeTrust.Runnable.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Web.RazorWire;

public class RazorWireWebModule : IRunnableWebModule
{
    /// <summary>
    /// Ensures the application's MVC support level is at least ControllersWithViews.
    /// </summary>
    /// <param name="context">The startup context for the web module.</param>
    /// <param name="options">Web options to configure; may be modified to raise Mvc.MvcSupportLevel to ControllersWithViews if it is lower.</param>
    public void ConfigureWebOptions(StartupContext context, WebOptions options)
    {
        if (options.Mvc.MvcSupportLevel < MvcSupport.ControllersWithViews)
        {
            options.Mvc.MvcSupportLevel = MvcSupport.ControllersWithViews;
        }
    }

    public bool IncludeAsApplicationPart => true;

    /// <summary>
    /// Registers RazorWire services, enables output caching, and configures output cache options to include RazorWire policies.
    /// </summary>
    /// <param name="context">The startup context for the current module initialization.</param>
    /// <param name="services">The service collection to which RazorWire, output caching, and related options are added.</param>
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

    /// <summary>
    /// Registers this module's dependencies with the provided dependency builder.
    /// </summary>
    /// <param name="builder">The dependency builder used to declare other modules this module requires.</param>
    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
        // RazorWire depends on the core web functionality
    }

    /// <summary>
    /// Executes module-specific host configuration before application services are registered.
    /// </summary>
    /// <param name="context">The startup context providing environment and configuration for module initialization.</param>
    /// <param name="builder">The host builder to apply pre-service host configuration to.</param>
    /// <remarks>The default implementation does nothing.</remarks>
    public void ConfigureHostBeforeServices(StartupContext context, Microsoft.Extensions.Hosting.IHostBuilder builder)
    {
    }

    /// <summary>
    /// Provides a hook to modify the host builder after services have been registered.
    /// </summary>
    /// <param name="context">Startup context containing environment and module information.</param>
    /// <param name="builder">The <see cref="Microsoft.Extensions.Hosting.IHostBuilder"/> to configure.</param>
    public void ConfigureHostAfterServices(StartupContext context, Microsoft.Extensions.Hosting.IHostBuilder builder)
    {
    }

    /// <summary>
    /// Enables output caching in the application's request pipeline.
    /// </summary>
    /// <param name="context">Startup context providing environment and configuration for module initialization.</param>
    /// <param name="app">Application builder used to configure the HTTP request pipeline.</param>
    public void ConfigureWebApplication(StartupContext context, IApplicationBuilder app)
    {
        app.UseOutputCache();
    }

    /// <summary>
    /// Maps RazorWire HTTP endpoints into the application's endpoint route builder.
    /// </summary>
    /// <param name="context">The startup context providing environment and configuration for module initialization.</param>
    /// <param name="endpoints">The endpoint route builder to which RazorWire routes will be added.</param>
    public void ConfigureEndpoints(StartupContext context, IEndpointRouteBuilder endpoints)
    {
        endpoints.MapRazorWire();
    }
}