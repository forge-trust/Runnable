using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using ForgeTrust.Runnable.Core;
using ForgeTrust.Runnable.Web.Tests.SharedErrorPagesFixture;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ForgeTrust.Runnable.Web.Tests;

public sealed class ConventionalNotFoundPageTests
{
    [Fact]
    public async Task AutoMode_WithControllersWithViews_UsesFrameworkFallback()
    {
        await using var runningApp = await StartHostAsync(
            new PlainWebModule(),
            options => { options.Mvc = options.Mvc with { MvcSupportLevel = MvcSupport.ControllersWithViews }; },
            typeof(WebApplication).Assembly);

        using var response = await runningApp.Client.GetAsync(ConventionalNotFoundPageDefaults.ReservedNotFoundRoute);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Runnable default 404", html);
    }

    [Fact]
    public async Task AutoMode_WithControllers_DoesNotEnableConventionalNotFoundPage()
    {
        await using var runningApp = await StartHostAsync(
            new PlainWebModule(),
            options => { options.Mvc = options.Mvc with { MvcSupportLevel = MvcSupport.Controllers }; },
            typeof(WebApplication).Assembly);

        using var response = await runningApp.Client.GetAsync(ConventionalNotFoundPageDefaults.ReservedNotFoundRoute);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EnabledMode_UpgradesControllersOnlyApps_AndMapsReservedRoute()
    {
        await using var runningApp = await StartHostAsync(
            new PlainWebModule(),
            options =>
            {
                options.Mvc = options.Mvc with { MvcSupportLevel = MvcSupport.Controllers };
                options.Errors.UseConventionalNotFoundPage();
            },
            typeof(WebApplication).Assembly);

        using var response = await runningApp.Client.GetAsync(ConventionalNotFoundPageDefaults.ReservedNotFoundRoute);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Runnable default 404", html);
    }

    [Fact]
    public async Task DisabledMode_WithViews_DoesNotMapReservedRoute()
    {
        await using var runningApp = await StartHostAsync(
            new PlainWebModule(),
            options =>
            {
                options.Mvc = options.Mvc with { MvcSupportLevel = MvcSupport.ControllersWithViews };
                options.Errors.DisableNotFoundPage();
            },
            typeof(WebApplication).Assembly);

        using var response = await runningApp.Client.GetAsync(ConventionalNotFoundPageDefaults.ReservedNotFoundRoute);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task HtmlRequests_ToUnknownRoutes_RenderBranded404_AndPreserveStatus()
    {
        await using var runningApp = await StartHostAsync(
            new PlainWebModule(),
            options => { options.Mvc = options.Mvc with { MvcSupportLevel = MvcSupport.ControllersWithViews }; },
            typeof(WebApplication).Assembly);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/missing-framework-route?from=test");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        using var response = await runningApp.Client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Runnable default 404", html);
        Assert.Contains("/missing-framework-route", html);
    }

    [Fact]
    public async Task JsonRequests_ToUnknownRoutes_DoNotRenderBranded404()
    {
        await using var runningApp = await StartHostAsync(
            new PlainWebModule(),
            options => { options.Mvc = options.Mvc with { MvcSupportLevel = MvcSupport.ControllersWithViews }; },
            typeof(WebApplication).Assembly);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/missing-json-route");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await runningApp.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("Runnable default 404", body);
    }

    [Fact]
    public async Task HtmlRequests_ToNon404Responses_DoNotReExecuteConventionalNotFoundPage()
    {
        await using var runningApp = await StartHostAsync(
            new NonNotFoundHtmlResponseWebModule(),
            options => { options.Mvc = options.Mvc with { MvcSupportLevel = MvcSupport.ControllersWithViews }; },
            typeof(WebApplication).Assembly);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/html-401");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        using var response = await runningApp.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(string.Empty, body);
        Assert.False(response.Headers.Contains("X-Runnable-Reexecuted"));
    }

    [Fact]
    public async Task SharedRclView_IsUsed_WhenAppOverrideIsMissing()
    {
        await using var runningApp = await StartHostAsync(
            new SharedConsumerWebModule(),
            options => { options.Mvc = options.Mvc with { MvcSupportLevel = MvcSupport.ControllersWithViews }; },
            typeof(WebApplication).Assembly);

        using var response = await runningApp.Client.GetAsync(ConventionalNotFoundPageDefaults.ReservedNotFoundRoute);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Shared fixture 404", html);
        Assert.DoesNotContain("Runnable default 404", html);
    }

    [Fact]
    public async Task LocalAppView_Wins_OverSharedRclView()
    {
        await using var runningApp = await StartHostAsync(
            new LocalOverrideWebModule(),
            options => { options.Mvc = options.Mvc with { MvcSupportLevel = MvcSupport.ControllersWithViews }; });

        using var response = await runningApp.Client.GetAsync(ConventionalNotFoundPageDefaults.ReservedNotFoundRoute);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Local test assembly 404", html);
        Assert.DoesNotContain("Shared fixture 404", html);
    }

    private static async Task<RunningAppHandle> StartHostAsync<TModule>(
        TModule module,
        Action<WebOptions> configureOptions,
        Assembly? entryPointAssembly = null)
        where TModule : class, IRunnableWebModule, new()
    {
        var startup = new TestWebStartup<TModule>(module);
        startup.WithOptions(configureOptions);

        var context = new StartupContext([], module);
        if (entryPointAssembly is not null)
        {
            context.OverrideEntryPointAssembly = entryPointAssembly;
        }

        var builder = ((IRunnableStartup)startup).CreateHostBuilder(context);
        builder.ConfigureWebHost(webHost => webHost.UseUrls("http://127.0.0.1:0"));

        var host = builder.Build();
        await host.StartAsync();

        var addresses = host.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?.Addresses
            ?? [];

        var baseUrl = Assert.Single(addresses);
        return new RunningAppHandle(host, baseUrl);
    }

    private sealed class TestWebStartup<TModule> : WebStartup<TModule>
        where TModule : class, IRunnableWebModule, new()
    {
        private readonly TModule _module;

        public TestWebStartup(TModule module)
        {
            _module = module;
        }

        protected override TModule CreateRootModule() => _module;
    }

    private abstract class BaseTestWebModule : IRunnableWebModule
    {
        public virtual bool IncludeAsApplicationPart => false;

        public virtual void ConfigureWebOptions(StartupContext context, WebOptions options)
        {
        }

        public virtual void ConfigureServices(StartupContext context, IServiceCollection services)
        {
        }

        public virtual void RegisterDependentModules(ModuleDependencyBuilder builder)
        {
        }

        public virtual void ConfigureHostBeforeServices(StartupContext context, IHostBuilder builder)
        {
        }

        public virtual void ConfigureHostAfterServices(StartupContext context, IHostBuilder builder)
        {
        }

        public virtual void ConfigureWebApplication(StartupContext context, IApplicationBuilder app)
        {
        }

        public virtual void ConfigureEndpoints(StartupContext context, IEndpointRouteBuilder endpoints)
        {
        }
    }

    private sealed class PlainWebModule : BaseTestWebModule;

    private sealed class SharedConsumerWebModule : BaseTestWebModule
    {
        public override void RegisterDependentModules(ModuleDependencyBuilder builder)
        {
            builder.AddModule<SharedErrorPagesFixtureModule>();
        }
    }

    private sealed class NonNotFoundHtmlResponseWebModule : BaseTestWebModule
    {
        public override void ConfigureWebApplication(StartupContext context, IApplicationBuilder app)
        {
            app.Use(
                async (httpContext, next) =>
                {
                    var reExecuteFeature = httpContext.Features.Get<IStatusCodeReExecuteFeature>();
                    if (reExecuteFeature?.OriginalStatusCode == StatusCodes.Status401Unauthorized)
                    {
                        httpContext.Response.Headers["X-Runnable-Reexecuted"] = "true";
                    }

                    await next();
                });
        }

        public override void ConfigureEndpoints(StartupContext context, IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet(
                "/html-401",
                httpContext =>
                {
                    httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    httpContext.Response.ContentType = "text/html";
                    return Task.CompletedTask;
                });
        }
    }

    private sealed class LocalOverrideWebModule : BaseTestWebModule
    {
        public override bool IncludeAsApplicationPart => true;

        public override void RegisterDependentModules(ModuleDependencyBuilder builder)
        {
            builder.AddModule<SharedErrorPagesFixtureModule>();
        }
    }

    private sealed class RunningAppHandle : IAsyncDisposable
    {
        public RunningAppHandle(IHost host, string baseUrl)
        {
            Host = host;
            BaseUrl = baseUrl;
            Client = new HttpClient
            {
                BaseAddress = new Uri(baseUrl)
            };
        }

        public IHost Host { get; }

        public string BaseUrl { get; }

        public HttpClient Client { get; }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Host.StopAsync();
            Host.Dispose();
        }
    }
}
