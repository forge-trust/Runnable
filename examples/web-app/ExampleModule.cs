using ForgeTrust.Runnable.Core;
using ForgeTrust.Runnable.Web;
using ForgeTrust.Runnable.Web.OpenApi;
using ForgeTrust.Runnable.Web.Scalar;

public class ExampleModule : IRunnableWebModule
{
    public void ConfigureServices(StartupContext context, IServiceCollection services)
    {
        // Register services for the application here if needed
    }

    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
        builder.AddModule<RunnableWebScalarModule>();
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

    public void ConfigureEndpoints(StartupContext context, IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/module", () => "Hello from the example module!");
    }
}
