using Autofac;
using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ForgeTrust.Runnable.Dependency.Autofac.Tests;

public class RunnableAutofacModuleTests
{
    [Fact]
    public void RunnableAutofacModule_SatisfiesRunnableModuleWithoutMicrosoftDiRegistrations()
    {
        var module = new NoopAutofacModule();
        var services = new ServiceCollection();
        var context = new StartupContext([], new RootAutofacHostModule());

        module.ConfigureServices(context, services);
        module.RegisterDependentModules(new ModuleDependencyBuilder());

        Assert.IsAssignableFrom<IRunnableModule>(module);
        Assert.Empty(services);
    }

    private sealed class NoopAutofacModule : RunnableAutofacModule
    {
    }
}

public class RunnableAutofacHostModuleTests
{
    [Fact]
    public void ConfigureHostBeforeServices_InstallsAutofacServiceProviderFactory()
    {
        var module = new RootAutofacHostModule();
        var context = new StartupContext([], module);
        var builder = Host.CreateDefaultBuilder();

        module.ConfigureHostBeforeServices(context, builder);
        builder.ConfigureContainer<ContainerBuilder>(container =>
            container.RegisterType<BeforeServicesAutofacService>().As<IBeforeServicesAutofacService>());

        using var host = builder.Build();

        Assert.IsType<BeforeServicesAutofacService>(
            host.Services.GetRequiredService<IBeforeServicesAutofacService>());
    }

    [Fact]
    public void ConfigureHostAfterServices_RegistersAutofacCompatibleDependentModulesFromStartupContext()
    {
        var rootModule = new RootAutofacHostModule();
        var context = new StartupContext([], rootModule);
        var startup = new TestStartup(rootModule);

        using var host = ((IRunnableStartup)startup).CreateHostBuilder(context).Build();

        Assert.IsType<DependentAutofacService>(
            host.Services.GetRequiredService<IDependentAutofacService>());
        Assert.IsType<RootAutofacService>(
            host.Services.GetRequiredService<IRootAutofacService>());
    }
}

public class RunnableAutofacExtensionsTests
{
    [Fact]
    public void RegisterImplementations_RegistersConcreteNonAbstractImplementationsFromInterfaceAssembly()
    {
        var builder = new ContainerBuilder();

        builder.RegisterImplementations<IScannedAutofacService>()
            .As<IScannedAutofacService>();

        using var container = builder.Build();

        var services = container.Resolve<IEnumerable<IScannedAutofacService>>().ToArray();

        Assert.Contains(services, service => service is FirstScannedAutofacService);
        Assert.Contains(services, service => service is SecondScannedAutofacService);
        Assert.DoesNotContain(services, service => service.GetType().IsAbstract);
        Assert.DoesNotContain(services, service => service.GetType() == typeof(UnrelatedAutofacService));
    }
}

public sealed class TestStartup : RunnableStartup<RootAutofacHostModule>
{
    private readonly RootAutofacHostModule _rootModule;

    public TestStartup(RootAutofacHostModule rootModule)
    {
        _rootModule = rootModule;
    }

    protected override RootAutofacHostModule CreateRootModule() => _rootModule;

    protected override void ConfigureServicesForAppType(StartupContext context, IServiceCollection services)
    {
    }
}

public sealed class RootAutofacHostModule : RunnableAutofacHostModule
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<RootAutofacService>().As<IRootAutofacService>();
    }

    public override void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
        builder.AddModule<DependentAutofacModule>();
        builder.AddModule<PlainRunnableModule>();
    }
}

public sealed class DependentAutofacModule : RunnableAutofacModule
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<DependentAutofacService>().As<IDependentAutofacService>();
    }
}

public sealed class PlainRunnableModule : IRunnableModule
{
    public void ConfigureServices(StartupContext context, IServiceCollection services)
    {
    }

    public void RegisterDependentModules(ModuleDependencyBuilder builder)
    {
    }
}

internal interface IBeforeServicesAutofacService
{
}

internal sealed class BeforeServicesAutofacService : IBeforeServicesAutofacService
{
}

internal interface IRootAutofacService
{
}

internal sealed class RootAutofacService : IRootAutofacService
{
}

internal interface IDependentAutofacService
{
}

internal sealed class DependentAutofacService : IDependentAutofacService
{
}

internal interface IScannedAutofacService
{
}

internal sealed class FirstScannedAutofacService : IScannedAutofacService
{
}

internal sealed class SecondScannedAutofacService : IScannedAutofacService
{
}

internal abstract class AbstractScannedAutofacService : IScannedAutofacService
{
}

internal sealed class UnrelatedAutofacService
{
}
