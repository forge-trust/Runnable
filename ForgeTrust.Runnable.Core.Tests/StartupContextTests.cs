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

    [Theory]
    [InlineData(ConsoleOutputMode.Default, 0)]
    [InlineData(ConsoleOutputMode.CommandFirst, 1)]
    public void ConsoleOutputMode_NumericValues_AreStable(ConsoleOutputMode value, int expected)
    {
        Assert.Equal(expected, (int)value);
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
