using FakeItEasy;
using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Microsoft.Extensions.Hosting;

namespace ForgeTrust.Runnable.Config.Tests;

public class TestConfig : Config<TestConfig>
{
}

public class TestHostModule : IRunnableHostModule
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

public abstract class AbstractConfig : IConfig
{
    public bool IsDefaultValue => true;

    public void Init(IConfigManager configManager, IEnvironmentProvider environmentProvider, string keyPath)
    {
    }
}

public interface IInterfaceConfig : IConfig
{
}

public class GenericConfig<T> : IConfig
{
    public bool IsDefaultValue => true;

    public void Init(IConfigManager configManager, IEnvironmentProvider environmentProvider, string keyPath)
    {
        _ = typeof(T);
    }
}

public class TrackingTestConfig : Config<TrackingTestConfig>
{
    public bool InitCalled { get; private set; }

    internal override void Init(IConfigManager configManager, IEnvironmentProvider environmentProvider, string keyPath)
    {
        InitCalled = true;
        base.Init(configManager, environmentProvider, keyPath);
    }
}

public class RunnableConfigModuleTests
{
    [Fact]
    public void ConfigureServices_AddsRequiredServices()
    {
        var services = new ServiceCollection();
        var rootModule = A.Fake<IRunnableHostModule>();
        var context = new StartupContext([], rootModule);
        var module = new RunnableConfigModule();

        module.ConfigureServices(context, services);

        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IConfigManager) && d.ImplementationType == typeof(DefaultConfigManager));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IEnvironmentConfigProvider)
                 && d.ImplementationType == typeof(EnvironmentConfigProvider));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IConfigFileLocationProvider)
                 && d.ImplementationType == typeof(DefaultConfigFileLocationProvider));
        Assert.Contains(
            services,
            d => d.ServiceType == typeof(IConfigProvider) && d.ImplementationType == typeof(FileBasedConfigProvider));

        // One registration for the module's assembly scanning task
        Assert.Single(context.CustomRegistrations);
    }

    [Fact]
    public void CustomRegistrationTask_ScansAssembliesAndRegistersConfigs()
    {
        var services = new ServiceCollection();
        var rootModule = new TestHostModule();
        services.AddSingleton<IEnvironmentProvider>(A.Fake<IEnvironmentProvider>());

        var context = new StartupContext([], rootModule)
        {
            OverrideEntryPointAssembly = typeof(RunnableConfigModuleTests).Assembly
        };
        context.Dependencies.AddModule<TestHostModule>();

        var module = new RunnableConfigModule();
        var moduleBuilder = new ModuleDependencyBuilder();
        module.RegisterDependentModules(moduleBuilder);

        module.ConfigureServices(context, services);
        var registrationTask = context.CustomRegistrations[0];

        // Invoke the task
        registrationTask(services);

        // Verify it registered TestConfig and TrackingTestConfig
        Assert.Contains(services, d => d.ServiceType == typeof(TestConfig));
        Assert.Contains(services, d => d.ServiceType == typeof(TrackingTestConfig));

        // Test the factory activation
        var configManager = A.Fake<IConfigManager>();
        var envProvider = A.Fake<IEnvironmentProvider>();
        services.AddSingleton<IConfigManager>(configManager);
        services.AddSingleton<IEnvironmentProvider>(envProvider);
        services.AddSingleton<ILogger<DefaultConfigManager>>(A.Fake<ILogger<DefaultConfigManager>>());

        var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<TrackingTestConfig>();

        Assert.NotNull(resolved);
        Assert.True(resolved.InitCalled);

        // Verify Init was called via ConfigManager too
        A.CallTo(() => configManager.GetValue<TrackingTestConfig>(A<string>._, A<string>._))
            .MustHaveHappened();
    }
}
