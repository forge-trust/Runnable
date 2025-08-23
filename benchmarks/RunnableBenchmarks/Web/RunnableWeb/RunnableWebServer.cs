using ForgeTrust.Runnable.Core;
using ForgeTrust.Runnable.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace RunnableBenchmarks.Web.RunnableWeb;

public class RunnableWebServer
{
    private IHost? _host;

    public async Task StartAsync()
    {
        var startup = new BenchmarkWebStartup()
            .WithOptions(options =>
            {
                options.MapEndpoints = endpoints =>
                {
                    endpoints.MapGet("/hello", () => "Hello World!");
                };
            });

        // We need to create the host builder manually to get access to start/stop.
        var context = new StartupContext([], new RunnableBenchmarkModule());
        _host = ((IRunnableStartup)startup).CreateHostBuilder(context).Build();

        await _host.StartAsync();
    }

    public async Task StopAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    private class BenchmarkWebStartup : WebStartup<RunnableBenchmarkModule>;

    private class RunnableBenchmarkModule : IRunnableWebModule
    {
        public void ConfigureServices(StartupContext context, IServiceCollection services)
        {
        }

        public void RegisterDependentModules(ModuleDependencyBuilder builder)
        {
        }

        public void ConfigureHostBeforeServices(StartupContext context, IHostBuilder builder)
        {
            builder.ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls("http://localhost:5000");
            });
        }

        public void ConfigureHostAfterServices(StartupContext context, IHostBuilder builder)
        {
        }

        public void ConfigureWebApplication(StartupContext context, IApplicationBuilder app)
        {
        }
    }
}
