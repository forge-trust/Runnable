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
