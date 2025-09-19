using ForgeTrust.Runnable.Core.Defaults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ForgeTrust.Runnable.Core.Tests;

public class DefaultEnvironmentProviderTests
{

    [Theory]
    [InlineData("DOTNET_ENVIRONMENT", "Production", false)]
    [InlineData("DOTNET_ENVIRONMENT","Staging", false)]
    [InlineData("DOTNET_ENVIRONMENT", "Development", true)]
    [InlineData("ASPNETCORE_ENVIRONMENT", "Production", false)]
    [InlineData("ASPNETCORE_ENVIRONMENT", "Staging", false)]
    [InlineData("ASPNETCORE_ENVIRONMENT", "Development", true)]
    public void DefaultEnvironmentProvider_HandlesDevelopmentFlag(string envVariable, string environment, bool isDev)
    {
        // Clear any existing env vars to avoid test pollution
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", null);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);

        Environment.SetEnvironmentVariable(envVariable, environment);

        var root = new NoHostModule();
        var startup = new CaptureIsDevStartup();
        var context = new StartupContext([], root);

        using var host = ((IRunnableStartup)startup).CreateHostBuilder(context).Build();

        Assert.Equal(isDev, startup.CapturedIsDevelopment);
        // Ensure the provider resolved from DI matches what the module registered
        var provider = host.Services.GetRequiredService<IEnvironmentProvider>();
        Assert.IsType<DefaultEnvironmentProvider>(provider);
        Assert.Equal(startup.CapturedIsDevelopment, provider.IsDevelopment);
    }

    [Theory]
    [InlineData("foo", true)]
    [InlineData("Development", false)]
    public void ModulesCanOverrideEnvironmentProvider_AndContextIsDevelopmentReflectsOverride(
        string environment,
        bool isDev)
    {
        var root = new EnvOverrideRootModule()
        {
            Environment = environment,
            IsDev = isDev
        };
        var startup = new CaptureIsDevStartup();
        var context = new StartupContext([], root);

        using var host = ((IRunnableStartup)startup).CreateHostBuilder(context).Build();

        Assert.Equal(isDev, startup.CapturedIsDevelopment);
        // Ensure the provider resolved from DI matches what the module registered
        var provider = host.Services.GetRequiredService<IEnvironmentProvider>();
        Assert.IsType<TestEnvironmentProvider>(provider);
        Assert.Equal(startup.CapturedIsDevelopment, provider.IsDevelopment);
    }

    [Fact]
    public void DefaultEnvironmentProvider_Prefers_ASPNETCORE_ENVIRONMENT_Over_DOTNET_ENVIRONMENT()
    {
        // Clear any existing env vars to avoid test pollution
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", null);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);

        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "foo");

        var provider = new DefaultEnvironmentProvider();

        Assert.Equal("foo", provider.Environment);
        Assert.False(provider.IsDevelopment);
    }


    private class EnvOverrideRootModule : IRunnableHostModule
    {
        public string Environment { get; init; }
        public bool IsDev { get; init; }

        public void ConfigureServices(StartupContext context, IServiceCollection services)
        {
            services.AddSingleton<IEnvironmentProvider>(new TestEnvironmentProvider(Environment, IsDev));
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

    private class CaptureIsDevStartup : RunnableStartup<EnvOverrideRootModule>
    {
        public bool CapturedIsDevelopment { get; private set; }

        protected override void ConfigureServicesForAppType(StartupContext context, IServiceCollection services)
        {
            // Capture the IsDevelopment flag as seen by app-type configuration
            CapturedIsDevelopment = context.IsDevelopment;
        }
    }

    private class TestEnvironmentProvider(string environment, bool isDevelopment) : IEnvironmentProvider
    {
        public string Environment => environment;
        public bool IsDevelopment => isDevelopment;
    }
}
