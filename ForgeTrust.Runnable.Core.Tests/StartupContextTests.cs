using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class StartupContextTests
{
    [Fact]
    public void GetDependencies_ReturnsAddedModules()
    {
        var context = new StartupContext([], new DummyModule());
        context.Dependencies.AddModule<DummyModule>();

        var deps = context.GetDependencies();

        Assert.Single(deps);
        Assert.IsType<DummyModule>(deps[0]);
    }

    [Fact]
    public void ConsoleOutputMode_DefaultsToDefault()
    {
        var context = new StartupContext([], new DummyModule());

        Assert.Equal(ConsoleOutputMode.Default, context.ConsoleOutputMode);
    }

    [Fact]
    public void ConsoleOutputMode_CanBeConfigured()
    {
        var context = new StartupContext([], new DummyModule())
        {
            ConsoleOutputMode = ConsoleOutputMode.CommandFirst
        };

        Assert.Equal(ConsoleOutputMode.CommandFirst, context.ConsoleOutputMode);
    }

    [Fact]
    public void ApplicationName_DefaultsToRootModuleAssemblyName()
    {
        var context = new StartupContext([], new DummyModule());

        Assert.Equal(typeof(DummyModule).Assembly.GetName().Name, context.ApplicationName);
    }

    [Fact]
    public void ApplicationName_CanBeProvidedThroughConstructor()
    {
        var context = new StartupContext([], new DummyModule(), "CustomApp");

        Assert.Equal("CustomApp", context.ApplicationName);
    }

    [Fact]
    public void ApplicationName_CanBeOverriddenWithWithExpression()
    {
        var context = new StartupContext([], new DummyModule());

        var renamed = context with { ApplicationName = "CustomApp" };

        Assert.Equal("CustomApp", renamed.ApplicationName);
        Assert.Equal(context.RootModuleAssembly, renamed.RootModuleAssembly);
        Assert.Equal(context.HostApplicationName, renamed.HostApplicationName);
    }

    [Fact]
    public void HostApplicationName_DefaultsToRootModuleAssemblyName()
    {
        var context = new StartupContext([], new DummyModule());

        Assert.Equal(typeof(DummyModule).Assembly.GetName().Name, context.HostApplicationName);
    }

    [Fact]
    public void HostApplicationName_UsesOverrideEntryPointAssemblyName()
    {
        var context = new StartupContext([], new DummyModule())
        {
            OverrideEntryPointAssembly = typeof(string).Assembly
        };

        Assert.Equal(typeof(string).Assembly.GetName().Name, context.HostApplicationName);
    }

    private class DummyModule : IRunnableHostModule
    {
        public void ConfigureServices(StartupContext context, IServiceCollection services)
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
