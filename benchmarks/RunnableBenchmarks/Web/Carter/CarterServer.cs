using Carter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SimpleApiController;

namespace RunnableBenchmarks.Web.Carter;

public class CarterServer
{
    private WebApplication? _app;

    public async Task StartMinimalAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddCarter();
        _app = builder.Build();
        _app.MapCarter();

        await _app.StartAsync();
    }

    public async Task StartControllersAsync()
    {
        var builder = WebApplication.CreateBuilder();
        var mvc = builder.Services.AddControllers();
        mvc.AddApplicationPart(typeof(HelloController).Assembly);

        builder.Services.AddCarter();

        _app = builder.Build();

        _app.MapControllers();
        _app.MapCarter();

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

public class HelloModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/hello", () => "Hello World!");
    }
}

