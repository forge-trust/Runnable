using System.Net;
using System.Net.Http.Headers;
using ForgeTrust.Runnable.Core;
using ForgeTrust.Runnable.Web.Tests.SharedErrorPagesFixture;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

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
        Assert.Equal(NonNotFoundHtmlResponseWebModule.OriginalUnauthorizedHtmlBody, body);
        Assert.False(response.Headers.Contains("X-Runnable-Reexecuted"));
    }

    [Fact]
    public async Task HtmlRequests_ToEmptyNon404Responses_DoNotReExecuteConventionalNotFoundPage()
    {
        await using var runningApp = await StartHostAsync(
            new NonNotFoundHtmlResponseWebModule(),
            options => { options.Mvc = options.Mvc with { MvcSupportLevel = MvcSupport.ControllersWithViews }; },
            typeof(WebApplication).Assembly);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/empty-401");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));

        using var response = await runningApp.Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(string.Empty, body);
        Assert.False(response.Headers.Contains("X-Runnable-Reexecuted"));
    }

    [Fact]
    public async Task DirectRequests_ToReservedNon404Routes_Return404WithoutRenderingFallback()
    {
        await using var runningApp = await StartHostAsync(
            new PlainWebModule(),
            options => { options.Mvc = options.Mvc with { MvcSupportLevel = MvcSupport.ControllersWithViews }; },
            typeof(WebApplication).Assembly);

        using var response = await runningApp.Client.GetAsync("/_runnable/errors/401");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(string.Empty, body);
    }

    [Fact]
    public void ShouldApplyConventionalNotFoundPage_ReturnsFalse_ForNonGetRequests()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Headers.Accept = "text/html";

        var shouldApply = WebStartup<PlainWebModule>.ShouldApplyConventionalNotFoundPage(httpContext);

        Assert.False(shouldApply);
    }

    [Fact]
    public void ShouldApplyConventionalNotFoundPage_ReturnsTrue_ForHeadRequestsAcceptingHtml()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Head;
        httpContext.Request.Headers.Accept = "text/html";

        var shouldApply = WebStartup<PlainWebModule>.ShouldApplyConventionalNotFoundPage(httpContext);

        Assert.True(shouldApply);
    }

    [Fact]
    public void ShouldApplyConventionalNotFoundPage_ReturnsFalse_ForReservedRoutes()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = ConventionalNotFoundPageDefaults.ReservedNotFoundRoute;
        httpContext.Request.Headers.Accept = "text/html";

        var shouldApply = WebStartup<PlainWebModule>.ShouldApplyConventionalNotFoundPage(httpContext);

        Assert.False(shouldApply);
    }

    [Fact]
    public void ShouldApplyConventionalNotFoundPage_ReturnsFalse_WhenRequestDoesNotPreferHtml()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Headers.Accept = "application/json";

        var shouldApply = WebStartup<PlainWebModule>.ShouldApplyConventionalNotFoundPage(httpContext);

        Assert.False(shouldApply);
    }

    [Fact]
    public void GetReservedRouteStatusCode_ReturnsNull_WhenRouteValueMissing()
    {
        var httpContext = new DefaultHttpContext();

        var statusCode = WebStartup<PlainWebModule>.GetReservedRouteStatusCode(httpContext);

        Assert.Null(statusCode);
    }

    [Fact]
    public void GetReservedRouteStatusCode_ReturnsInt_WhenRouteValueIsAlreadyAnInt()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["statusCode"] = 404;

        var statusCode = WebStartup<PlainWebModule>.GetReservedRouteStatusCode(httpContext);

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
    }

    [Fact]
    public void GetReservedRouteStatusCode_ReturnsNull_ForUnsupportedRouteValueTypes()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["statusCode"] = new object();

        var statusCode = WebStartup<PlainWebModule>.GetReservedRouteStatusCode(httpContext);

        Assert.Null(statusCode);
    }

    [Fact]
    public void GetReservedRouteStatusCode_ReturnsParsedInt_WhenRouteValueIsAString()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues["statusCode"] = "404";

        var statusCode = WebStartup<PlainWebModule>.GetReservedRouteStatusCode(httpContext);

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
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

    [Fact]
    public void ValidateConfiguredViews_ThrowsWithSearchedLocations_WhenNoViewsResolve()
    {
        var renderer = CreateRenderer(
            new StubCompositeViewEngine(
                appResult: ViewEngineResult.NotFound(ConventionalNotFoundPageDefaults.AppViewPath, ["/Views/Shared/404.cshtml"]),
                frameworkResult: ViewEngineResult.NotFound(ConventionalNotFoundPageDefaults.FrameworkFallbackViewPath, ["/Views/_Runnable/Errors/404.cshtml"])));

        var exception = Assert.Throws<InvalidOperationException>(() => renderer.ValidateConfiguredViews());

        Assert.Contains("/Views/Shared/404.cshtml", exception.Message);
        Assert.Contains("/Views/_Runnable/Errors/404.cshtml", exception.Message);
    }

    [Fact]
    public void ValidateConfiguredViews_ThrowsFallbackMessage_WhenViewEngineReportsNoLocations()
    {
        var renderer = CreateRenderer(
            new StubCompositeViewEngine(
                appResult: ViewEngineResult.NotFound(ConventionalNotFoundPageDefaults.AppViewPath, []),
                frameworkResult: ViewEngineResult.NotFound(ConventionalNotFoundPageDefaults.FrameworkFallbackViewPath, [])));

        var exception = Assert.Throws<InvalidOperationException>(() => renderer.ValidateConfiguredViews());

        Assert.Contains("No Razor view locations were reported.", exception.Message);
    }

    [Fact]
    public async Task RenderAsync_UsesDefault404Status_WhenNoRouteStatusExists()
    {
        var executor = new CapturingViewResultExecutor();
        var renderer = CreateRenderer(
            new StubCompositeViewEngine(
                appResult: ViewEngineResult.Found(ConventionalNotFoundPageDefaults.AppViewPath, new StubView(ConventionalNotFoundPageDefaults.AppViewPath))),
            executor);
        var httpContext = new DefaultHttpContext();

        await renderer.RenderAsync(httpContext);

        Assert.Equal(StatusCodes.Status404NotFound, executor.Model?.StatusCode);
    }

    [Fact]
    public async Task RenderAsync_UsesIntRouteStatusCode_WhenPresent()
    {
        var executor = new CapturingViewResultExecutor();
        var renderer = CreateRenderer(
            new StubCompositeViewEngine(
                appResult: ViewEngineResult.Found(ConventionalNotFoundPageDefaults.AppViewPath, new StubView(ConventionalNotFoundPageDefaults.AppViewPath))),
            executor);
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        routeData.Values["statusCode"] = StatusCodes.Status401Unauthorized;
        httpContext.Features.Set<IRoutingFeature>(new StubRoutingFeature(routeData));

        await renderer.RenderAsync(httpContext);

        Assert.Equal(StatusCodes.Status401Unauthorized, executor.Model?.StatusCode);
    }

    [Fact]
    public async Task RenderAsync_FallsBackTo404_WhenRouteStatusTypeIsUnsupported()
    {
        var executor = new CapturingViewResultExecutor();
        var renderer = CreateRenderer(
            new StubCompositeViewEngine(
                appResult: ViewEngineResult.Found(ConventionalNotFoundPageDefaults.AppViewPath, new StubView(ConventionalNotFoundPageDefaults.AppViewPath))),
            executor);
        var httpContext = new DefaultHttpContext();
        var routeData = new RouteData();
        routeData.Values["statusCode"] = new object();
        httpContext.Features.Set<IRoutingFeature>(new StubRoutingFeature(routeData));

        await renderer.RenderAsync(httpContext);

        Assert.Equal(StatusCodes.Status404NotFound, executor.Model?.StatusCode);
    }

    private static async Task<RunningAppHandle> StartHostAsync<TModule>(
        TModule module,
        Action<WebOptions> configureOptions,
        System.Reflection.Assembly? entryPointAssembly = null)
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

    private static ConventionalNotFoundPageRenderer CreateRenderer(
        ICompositeViewEngine viewEngine,
        IActionResultExecutor<ViewResult>? executor = null)
    {
        return new ConventionalNotFoundPageRenderer(
            executor ?? new CapturingViewResultExecutor(),
            viewEngine,
            new EmptyModelMetadataProvider(),
            NullLogger<ConventionalNotFoundPageRenderer>.Instance);
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
        public const string OriginalUnauthorizedHtmlBody = "<p>original 401 body</p>";

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
                    return httpContext.Response.WriteAsync(OriginalUnauthorizedHtmlBody);
                });

            endpoints.MapGet(
                "/empty-401",
                httpContext =>
                {
                    httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
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

    private sealed class CapturingViewResultExecutor : IActionResultExecutor<ViewResult>
    {
        public NotFoundPageModel? Model { get; private set; }

        public Task ExecuteAsync(ActionContext context, ViewResult result)
        {
            Model = Assert.IsType<NotFoundPageModel>(result.ViewData?.Model);
            return Task.CompletedTask;
        }
    }

    private sealed class StubCompositeViewEngine : ICompositeViewEngine
    {
        private readonly ViewEngineResult _appResult;
        private readonly ViewEngineResult _frameworkResult;

        public StubCompositeViewEngine(ViewEngineResult appResult, ViewEngineResult? frameworkResult = null)
        {
            _appResult = appResult;
            _frameworkResult = frameworkResult ?? ViewEngineResult.NotFound(
                ConventionalNotFoundPageDefaults.FrameworkFallbackViewPath,
                []);
        }

        public IReadOnlyList<IViewEngine> ViewEngines => [];

        public ViewEngineResult FindView(ActionContext context, string viewName, bool isMainPage)
        {
            return ViewEngineResult.NotFound(viewName, []);
        }

        public ViewEngineResult GetView(string? executingFilePath, string viewPath, bool isMainPage)
        {
            return viewPath switch
            {
                var path when path == ConventionalNotFoundPageDefaults.AppViewPath => _appResult,
                var path when path == ConventionalNotFoundPageDefaults.FrameworkFallbackViewPath => _frameworkResult,
                _ => ViewEngineResult.NotFound(viewPath, [])
            };
        }
    }

    private sealed class StubView(string path) : IView
    {
        public string Path { get; } = path;

        public Task RenderAsync(ViewContext context) => Task.CompletedTask;
    }

    private sealed class StubRoutingFeature(RouteData routeData) : IRoutingFeature
    {
        public RouteData? RouteData { get; set; } = routeData;
    }
}
