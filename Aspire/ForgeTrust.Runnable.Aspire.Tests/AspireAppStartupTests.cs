using System.Reflection;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using ForgeTrust.Runnable.Aspire;
using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class AspireAppStartupTests
{
    [Fact]
    public void GetComponentTypes_FiltersToConcreteAspireComponents()
    {
        // Arrange
        var startup = CreateStartupInstance();
        var getComponentTypesMethod = startup.GetType().GetMethod(
            "GetComponentTypes",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(getComponentTypesMethod);

        // Act
        var types = (IReadOnlyList<Type>)getComponentTypesMethod!.Invoke(
            startup,
            [typeof(AspireAppStartupTests).Assembly])!;

        // Assert
        Assert.Contains(typeof(ConcreteComponent), types);
        Assert.DoesNotContain(typeof(AbstractComponent), types);
        Assert.DoesNotContain(typeof(NonComponent), types);
        Assert.All(
            types,
            type =>
            {
                Assert.True(type.IsClass);
                Assert.False(type.IsAbstract);
                Assert.True(typeof(IAspireComponent).IsAssignableFrom(type));
            });
    }

    [Fact]
    public void ConfigureAdditionalServices_RegistersDiscoveredComponentsAsSingletons()
    {
        // Arrange
        var startup = CreateStartupInstance();
        var startupType = startup.GetType();
        var configureMethod = startupType.GetMethod(
            "ConfigureAdditionalServices",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var getComponentTypesMethod = startupType.GetMethod(
            "GetComponentTypes",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(configureMethod);
        Assert.NotNull(getComponentTypesMethod);

        var entryAssembly = typeof(AspireAppStartupTests).Assembly;
        var context = new StartupContext([], new DummyHostModule())
        {
            OverrideEntryPointAssembly = entryAssembly
        };
        var services = new ServiceCollection();

        // Act
        configureMethod!.Invoke(startup, [context, services]);
        var discoveredTypes = (IReadOnlyList<Type>)getComponentTypesMethod!.Invoke(startup, [entryAssembly])!;

        // Assert
        foreach (var type in discoveredTypes)
        {
            var descriptor = Assert.Single(services, descriptor => descriptor.ServiceType == type);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
            Assert.Equal(type, descriptor.ImplementationType);
        }
    }

    private static object CreateStartupInstance()
    {
        var genericTypeDefinition = typeof(AspireApp).Assembly.GetType(
            "ForgeTrust.Runnable.Aspire.AspireAppStartup`1",
            throwOnError: true)!;
        var closedType = genericTypeDefinition.MakeGenericType(typeof(DummyHostModule));
        return Activator.CreateInstance(closedType, nonPublic: true)!;
    }

    private sealed class DummyHostModule : IRunnableHostModule
    {
        public void ConfigureHostAfterServices(StartupContext context, IHostBuilder builder)
        {
        }

        public void ConfigureHostBeforeServices(StartupContext context, IHostBuilder builder)
        {
        }

        public void ConfigureServices(StartupContext context, IServiceCollection services)
        {
        }

        public void RegisterDependentModules(ModuleDependencyBuilder builder)
        {
        }
    }

    private abstract class AbstractComponent : IAspireComponent
    {
    }

    private sealed class ConcreteComponent : IAspireComponent<IResource>
    {
        public IResourceBuilder<IResource> Generate(AspireStartupContext context, IDistributedApplicationBuilder appBuilder)
        {
            throw new NotSupportedException("Not expected to execute during tests.");
        }
    }

    private sealed class NonComponent
    {
    }
}
