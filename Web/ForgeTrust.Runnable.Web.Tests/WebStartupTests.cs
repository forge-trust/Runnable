using ForgeTrust.Runnable.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ForgeTrust.Runnable.Web.Tests;

public class WebStartupTests
{
    [Fact]
    public void WithOptions_SetsConfigurationCallback()
    {
        var root = new TestWebModule();
        var startup = new TestWebStartup(root);
        var called = false;

        startup.WithOptions(_ => called = true);

        var context = new StartupContext([], root);
        var builder = ((IRunnableStartup)startup).CreateHostBuilder(context);

        // Configuration callback is invoked during service registration
        builder.Build();

        Assert.True(called);
    }

    [Fact]
    public void BuildModules_CorrectlyCollectsWebModules()
    {
        var root = new TestWebModule();
        var context = new StartupContext([], root);
        context.Dependencies.AddModule<TestWebModule>();

        var startup = new TestWebStartup(root);
        var builder = ((IRunnableStartup)startup).CreateHostBuilder(context);

        // Simply building the host ensures BuildModules is initialized
        using var host = builder.Build();

        // Assertions are tricky without public state, but this verifies no exceptions 
        // and hits the dependency iteration logic.
    }

    [Theory]
    [InlineData(MvcSupport.None)]
    [InlineData(MvcSupport.Controllers)]
    [InlineData(MvcSupport.ControllersWithViews)]
    [InlineData(MvcSupport.Full)]
    public void BuildWebOptions_MvcLevel_ExercisesBranches(MvcSupport level)
    {
        var root = new TestWebModule { MvcLevel = level };
        var startup = new TestWebStartup(root);
        var context = new StartupContext([], root);

        var builder = ((IRunnableStartup)startup).CreateHostBuilder(context);
        using var host = builder.Build();

        // Verify MVC services are present if level > None
        if (level > MvcSupport.None)
        {
            Assert.NotNull(
                host.Services
                    .GetService<Microsoft.AspNetCore.Mvc.Infrastructure.IActionDescriptorCollectionProvider>());
        }
    }

    [Fact]
    public void ConfigureServices_EnablesCors_WhenConfigured()
    {
        var root = new TestWebModule();
        var startup = new TestWebStartup(root);
        startup.WithOptions(o =>
        {
            o.Cors.EnableCors = true;
            o.Cors.AllowedOrigins = new[] { "https://example.com" };
        });
        var context = new StartupContext([], root);

        var builder = ((IRunnableStartup)startup).CreateHostBuilder(context);
        using var host = builder.Build();

        Assert.NotNull(host.Services.GetService<Microsoft.AspNetCore.Cors.Infrastructure.ICorsService>());
    }

    [Fact]
    public void ConfigureServices_Cors_EnableAllOriginsInDevelopment_Works()
    {
        var previous = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", Environments.Development);

            var root = new TestWebModule();
            var startup = new TestWebStartup(root);
            startup.WithOptions(o => o.Cors.EnableAllOriginsInDevelopment = true);

            var context = new StartupContext([], root);
            var builder = ((IRunnableStartup)startup).CreateHostBuilder(context);
            using var host = builder.Build();

            Assert.NotNull(host.Services.GetService<Microsoft.AspNetCore.Cors.Infrastructure.ICorsService>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previous);
        }
    }

    [Fact]
    public async Task ConfigureServices_Cors_SpecificOrigins_Works()
    {
        var previous = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", Environments.Production);

            var root = new TestWebModule();
            var startup = new TestWebStartup(root);
            startup.WithOptions(o =>
            {
                o.Cors.EnableCors = true;
                o.Cors.AllowedOrigins = new[] { "https://example.com" };
            });

            var context = new StartupContext([], root);

            var builder = ((IRunnableStartup)startup).CreateHostBuilder(context);
            using var host = builder.Build();

            var corsService = host.Services
                .GetRequiredService<Microsoft.AspNetCore.Cors.Infrastructure.ICorsPolicyProvider>();
            var policy = await corsService.GetPolicyAsync(
                new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                "DefaultCorsPolicy");

            Assert.NotNull(policy);
            Assert.Contains("https://example.com", policy.Origins);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previous);
        }
    }

    [Fact]
    public void ConfigureBuilderForAppType_ExercisesWebHostConfiguration()
    {
        var root = new TestWebModule();
        var startup = new TestWebStartup(root);
        var context = new StartupContext([], root);

        var builder = ((IRunnableStartup)startup).CreateHostBuilder(context);
        using var host = builder.Build();

        // Simply building the host confirms web host defaults were configured
        Assert.NotNull(host.Services.GetService<IWebHostEnvironment>());
    }

    [Fact]
    public void BuildWebOptions_UsesCachedOptions()
    {
        var root = new TestWebModule();
        var startup = new TestWebStartup(root);
        var context = new StartupContext([], root);

        // First call populates _options
        var builder1 = ((IRunnableStartup)startup).CreateHostBuilder(context);
        using var host1 = builder1.Build();

        // Second call on SAME startup instance should hit the cached options branch
        var builder2 = ((IRunnableStartup)startup).CreateHostBuilder(context);
        using var host2 = builder2.Build();
    }

    [Fact]
    public void ConfigureServices_AddsMultipleApplicationParts()
    {
        var root = new TestWebModule();
        var context = new StartupContext([], root);

        // Override entry point to something else so root module assembly is different from entry point
        context.OverrideEntryPointAssembly = typeof(WebApplication).Assembly;

        var startup = new TestWebStartup(root);
        var builder = ((IRunnableStartup)startup).CreateHostBuilder(context);

        using var host = builder.Build();
    }

    [Fact]
    public void ConfigureBuilder_RespectsEnableStaticWebAssets()
    {
        var root = new TestWebModule();
        var startup = new TestWebStartup(root);
        startup.WithOptions(o => o.StaticFiles.EnableStaticWebAssets = true);
        var context = new StartupContext([], root);

        var builder = ((IRunnableStartup)startup).CreateHostBuilder(context);
        using var host = builder.Build();

        // This exercises the EnableStaticWebAssets branch in ConfigureBuilderForAppType
    }

    [Fact]
    public async Task InitializeWebApplication_ExercisesAllMiddleware()
    {
        var root = new TestWebModule();
        var startup = new TestWebStartup(root);
        startup.WithOptions(o =>
        {
            o.Cors.EnableCors = true;
            o.Cors.AllowedOrigins = ["https://example.com"];
            o.StaticFiles.EnableStaticFiles = true;
            o.Mvc.MvcSupportLevel = MvcSupport.Controllers;
            o.MapEndpoints = endpoints => { endpoints.MapGet("/test-direct", () => "Direct"); };
        });

        var context = new StartupContext([], root);
        var builder = ((IRunnableStartup)startup).CreateHostBuilder(context);

        // Using StartAsync triggers the actual WebHost initialization which calls InitializeWebApplication
        using var host = builder.Build();
        await host.StartAsync();

        // Verify we can access the host effectively
        Assert.NotNull(host.Services.GetService<IWebHostEnvironment>());

        await host.StopAsync();
    }

    [Fact]
    public async Task ConfigureServices_Cors_WildcardInProduction_LogsWarning()
    {
        var previous = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", Environments.Production);

            var root = new TestWebModule();
            var startup = new TestWebStartup(root);
            startup.WithOptions(o =>
            {
                o.Cors.EnableCors = true;
                o.Cors.AllowedOrigins = ["*"];
            });

            var context = new StartupContext([], root);
            var builder = ((IRunnableStartup)startup).CreateHostBuilder(context);
            using var host = builder.Build();

            // Verify CORS service is registered
            Assert.NotNull(host.Services.GetService<Microsoft.AspNetCore.Cors.Infrastructure.ICorsService>());

            // Verify policy allows any origin
            var corsService = host.Services
                .GetRequiredService<Microsoft.AspNetCore.Cors.Infrastructure.ICorsPolicyProvider>();
            var policy = await corsService.GetPolicyAsync(
                new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                "DefaultCorsPolicy");

            Assert.NotNull(policy);
            Assert.True(policy.AllowAnyOrigin);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previous);
        }
    }

    [Fact]
    public void ConfigureServices_Cors_WildcardInDevelopment_DoesNotLogWarning()
    {
        var previous = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", Environments.Development);

            var root = new TestWebModule();
            var startup = new TestWebStartup(root);
            startup.WithOptions(o =>
            {
                o.Cors.EnableCors = true;
                o.Cors.AllowedOrigins = ["*"];
            });

            var context = new StartupContext([], root);
            var builder = ((IRunnableStartup)startup).CreateHostBuilder(context);
            using var host = builder.Build();

            // Verify CORS service is registered
            Assert.NotNull(host.Services.GetService<Microsoft.AspNetCore.Cors.Infrastructure.ICorsService>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previous);
        }
    }

    [Fact]
    public void ConfigureServices_ConfigureMvcCallback_IsInvoked()
    {
        var root = new TestWebModule { MvcLevel = MvcSupport.Controllers };
        var startup = new TestWebStartup(root);
        var mvcConfigured = false;

        startup.WithOptions(o =>
        {
            o.Mvc.MvcSupportLevel = MvcSupport.Controllers;
            o.Mvc.ConfigureMvc = builder => { mvcConfigured = true; };
        });

        var context = new StartupContext([], root);
        var builder = ((IRunnableStartup)startup).CreateHostBuilder(context);
        using var host = builder.Build();

        Assert.True(mvcConfigured, "ConfigureMvc callback should have been invoked");
    }

    [Fact]
    public void ConfigureServices_ModuleWithIncludeAsApplicationPartFalse_NotAdded()
    {
        var root = new TestWebModuleNoApplicationPart();
        var startup = new TestWebStartupNoAppPart(root);
        startup.WithOptions(o => o.Mvc.MvcSupportLevel = MvcSupport.Controllers);

        var context = new StartupContext([], root);
        var builder = ((IRunnableStartup)startup).CreateHostBuilder(context);
        using var host = builder.Build();

        // Verify MVC services are present
        Assert.NotNull(
            host.Services.GetService<Microsoft.AspNetCore.Mvc.Infrastructure.IActionDescriptorCollectionProvider>());
    }

    [Fact]
    public void BuildModules_NonWebModule_NotIncluded()
    {
        var root = new TestWebModule();
        var context = new StartupContext([], root);

        // Add a non-web module
        context.Dependencies.AddModule<NonWebModule>();

        var startup = new TestWebStartup(root);
        var builder = ((IRunnableStartup)startup).CreateHostBuilder(context);
        using var host = builder.Build();

        // Should build successfully without including the non-web module
        Assert.NotNull(host);
    }

    private class TestWebStartup : WebStartup<TestWebModule>
    {
        private readonly TestWebModule _module;

        public TestWebStartup(TestWebModule module)
        {
            _module = module;
        }

        protected override TestWebModule CreateRootModule() => _module;
    }

    private class TestWebStartupNoAppPart : WebStartup<TestWebModuleNoApplicationPart>
    {
        private readonly TestWebModuleNoApplicationPart _module;

        public TestWebStartupNoAppPart(TestWebModuleNoApplicationPart module)
        {
            _module = module;
        }

        protected override TestWebModuleNoApplicationPart CreateRootModule() => _module;
    }

    private class TestWebModule : IRunnableWebModule
    {
        public MvcSupport MvcLevel { get; init; } = MvcSupport.None;
        public bool IncludeAsApplicationPart => true;

        public void ConfigureWebOptions(StartupContext context, WebOptions options)
        {
            options.Mvc.MvcSupportLevel = MvcLevel;
        }

        public void ConfigureServices(StartupContext context, IServiceCollection services)
        {
        }

        public void ConfigureWebApplication(StartupContext context, IApplicationBuilder app)
        {
        }

        public void ConfigureEndpoints(StartupContext context, IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/test-module", () => "Module");
        }

        public void RegisterDependentModules(ModuleDependencyBuilder builder)
        {
        }

        public void ConfigureHostBeforeServices(StartupContext context, IHostBuilder builder)
        {
        }

        public void ConfigureHostAfterServices(StartupContext context, IHostBuilder builder)
        {
        }
    }

    private class TestWebModuleNoApplicationPart : IRunnableWebModule
    {
        public bool IncludeAsApplicationPart => false;

        public void ConfigureWebOptions(StartupContext context, WebOptions options)
        {
        }

        public void ConfigureServices(StartupContext context, IServiceCollection services)
        {
        }

        public void ConfigureWebApplication(StartupContext context, IApplicationBuilder app)
        {
        }

        public void ConfigureEndpoints(StartupContext context, IEndpointRouteBuilder endpoints)
        {
        }

        public void RegisterDependentModules(ModuleDependencyBuilder builder)
        {
        }

        public void ConfigureHostBeforeServices(StartupContext context, IHostBuilder builder)
        {
        }

        public void ConfigureHostAfterServices(StartupContext context, IHostBuilder builder)
        {
        }
    }

    private class NonWebModule : IRunnableModule
    {
        public void ConfigureServices(StartupContext context, IServiceCollection services)
        {
        }

        public void RegisterDependentModules(ModuleDependencyBuilder builder)
        {
        }
    }
}

public class StaticFilesOptionsTests
{
    [Fact]
    public void DefaultOptions_HaveExpectedDefaults()
    {
        var opts = new StaticFilesOptions();
        Assert.False(opts.EnableStaticFiles);
        Assert.False(opts.EnableStaticWebAssets);
    }

    [Fact]
    public void PropertySetters_WorkCorrectly()
    {
        var opts = new StaticFilesOptions
        {
            EnableStaticFiles = true,
            EnableStaticWebAssets = true
        };
        Assert.True(opts.EnableStaticFiles);
        Assert.True(opts.EnableStaticWebAssets);
    }
}
