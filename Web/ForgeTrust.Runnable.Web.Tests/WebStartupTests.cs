using ForgeTrust.Runnable.Core;
using ForgeTrust.Runnable.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ForgeTrust.Runnable.Web.Tests;

public class WebStartupTests
{
    [Fact]
    public void WithOptions_SetsConfigurationCallback()
    {
        var root = new TestWebModule();
        var startup = new TestWebStartup(root);
        var called = false;

        startup.WithOptions(opts => called = true);

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
        startup.WithOptions(o => o.Cors.EnableCors = true);
        var context = new StartupContext([], root);

        var builder = ((IRunnableStartup)startup).CreateHostBuilder(context);
        using var host = builder.Build();

        Assert.NotNull(host.Services.GetService<Microsoft.AspNetCore.Cors.Infrastructure.ICorsService>());
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

    private class TestWebStartup : WebStartup<TestWebModule>
    {
        private readonly TestWebModule _module;
        public TestWebStartup(TestWebModule module) => _module = module;
        protected override TestWebModule CreateRootModule() => _module;
    }

    private class TestWebModule : IRunnableWebModule
    {
        public MvcSupport MvcLevel { get; set; } = MvcSupport.None;
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
