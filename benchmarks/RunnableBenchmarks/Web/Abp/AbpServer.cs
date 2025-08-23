using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AspNetCore;
using Volo.Abp.Modularity;

namespace RunnableBenchmarks.Web.Abp;

public class AbpServer
{
    private WebApplication? _app;

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        await builder.Services.AddApplicationAsync<AbpBenchmarkModule>();
        _app = builder.Build();

        _app.MapGet("/hello", () => "Hello World!");
        await _app.InitializeApplicationAsync();
        await _app.StartAsync();
    }

    public async Task StopAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}

[DependsOn(typeof(AbpAspNetCoreModule))]
public class AbpBenchmarkModule : AbpModule
{
}
