using Microsoft.AspNetCore.Builder;

namespace RunnableBenchmarks.Web.NativeDotnet;

public class NativeDotnetServer
{
    private WebApplication? _app;

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        _app = builder.Build();
        _app.MapGet("/hello", () => "Hello World!");

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
