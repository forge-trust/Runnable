using ForgeTrust.Runnable.Core;
using ForgeTrust.Runnable.Web.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;

namespace ForgeTrust.Runnable.Web.Scalar;

public class RunnableWebScalarModule : IRunnableWebModule
{
    public void ConfigureServices(StartupContext context, IServiceCollection services)
    {
        
    }
    
    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
        builder.AddModule<RunnableWebOpenApiModule>();
    }

    public void ConfigureEndpoints(StartupContext context, IEndpointRouteBuilder endpoints)
    {
        endpoints.MapScalarApiReference(opt => opt.AddDocument(context.ApplicationName));
    }

    public void ConfigureHostBeforeServices(StartupContext context, IHostBuilder builder)
    {
    }

    public void ConfigureHostAfterServices(StartupContext context, IHostBuilder builder)
    {
    }

    public void ConfigureWebApplication(StartupContext context, IApplicationBuilder app)
    {
    }
}