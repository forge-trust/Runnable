using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Core;

public abstract class RunnableStartup
{
    protected static readonly Lazy<ILoggerFactory> StartupLoggerFactory =
        new(() => LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug)));

    /// <summary>
    /// Gets a logger instance named after the concrete startup type.
    /// </summary>
    /// <returns>An <see cref="ILogger"/> configured for the concrete startup type.</returns>
    protected ILogger GetStartupLogger()
    {
        return StartupLoggerFactory.Value.CreateLogger(GetType().Name);
    }
}

public abstract class RunnableStartup<TRootModule> : RunnableStartup, IRunnableStartup
    where TRootModule : IRunnableHostModule, new()
{
    /// <summary>
    /// Execute the startup sequence using the provided <see cref="StartupContext"/>.
    /// </summary>
    /// <param name="context">Startup configuration including application arguments, root module, and dependency/module registrations.</param>
    async Task IRunnableStartup.RunAsync(StartupContext context)
    {
        await RunAsync(context);
    }

    IHostBuilder IRunnableStartup.CreateHostBuilder(StartupContext context) => CreateHostBuilderCore(context);
    public Task RunAsync(string[] args) => RunAsync(new StartupContext(args, CreateRootModule()));

    /// <summary>
    /// Runs the application host for the provided startup context and manages its lifecycle until shutdown.
    /// </summary>
    /// <param name="context">The startup context containing application name, root module, dependencies, and configuration used to build the host.</param>
    /// <returns>A task that completes after the host stops or after shutdown/error handling finishes.</returns>
    /// <remarks>
    /// On cancellation, a warning is logged. On unhandled exceptions, a critical log is emitted and <see cref="Environment.ExitCode"/> is set to -100.
    /// </remarks>
    public async Task RunAsync(StartupContext context)
    {
        try
        {
            var host = CreateHost(context);
            var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger(GetType().Name);

            await host.RunAsync();

            logger.LogInformation("Run Exited - Shutting down");
        }
        catch (OperationCanceledException ex)
        {
            GetStartupLogger().LogWarning(ex, "Service(s) did not exit in a timely fashion.");
        }
        catch (Exception e)
        {
            GetStartupLogger().LogCritical(e, "Fatal Processing Error");
            Environment.ExitCode = -100;
        }
    }

    /// <summary>
/// Creates the application host for the provided startup context.
/// </summary>
/// <param name="context">The startup context containing configuration, modules, and dependencies used to construct the host.</param>
/// <returns>The constructed <see cref="IHost"/>.</returns>
private IHost CreateHost(StartupContext context) => ((IRunnableStartup)this).CreateHostBuilder(context).Build();

    /// <summary>
    /// Creates and configures an IHostBuilder based on the provided startup context.
    /// </summary>
    /// <param name="context">The startup context containing application name, root module, and dependency modules used to configure the host.</param>
    /// <returns>A configured <see cref="IHostBuilder"/> that reflects the context (including application name, registered modules, and service registrations).</returns>
    private IHostBuilder CreateHostBuilderCore(StartupContext context)
    {
        var builder = Host.CreateDefaultBuilder();

        // Ensure the host environment correctly reflects the application name from the context.
        // This is critical for features like Static Web Assets that rely on the application name to find manifests.
        builder.ConfigureAppConfiguration((hostingContext, _) =>
        {
            hostingContext.HostingEnvironment.ApplicationName = context.ApplicationName;
        });

        builder.ConfigureHostConfiguration(config =>
            config.AddInMemoryCollection(
                new Dictionary<string, string?> { [HostDefaults.ApplicationKey] = context.ApplicationName }));

        // Ensure internal services (like Default IEnvironmentProvider) are included first so external modules can override them.
        context.Dependencies.AddModule<Defaults.InternalServicesModule>();
        context.RootModule.RegisterDependentModules(context.Dependencies);

        ConfigureHostBeforeServicesCore(context, builder);

        builder.ConfigureServices(services =>
            ConfigureServicesInternal(context, services));

        ConfigureHostAfterServicesCore(context, builder);

        ConfigureBuilderForAppType(context, builder);

        return builder;
    }

    protected virtual TRootModule CreateRootModule() => new();

    private void ConfigureHostBeforeServicesCore(
        StartupContext context,
        IHostBuilder builder)
    {
        foreach (var dep in context.Dependencies.Modules)
        {
            if (dep is IRunnableHostModule host)
            {
                host.ConfigureHostBeforeServices(context, builder);
            }
        }

        context.RootModule.ConfigureHostBeforeServices(context, builder);
    }

    private void ConfigureHostAfterServicesCore(
        StartupContext context,
        IHostBuilder builder)
    {
        foreach (var dep in context.Dependencies.Modules)
        {
            if (dep is IRunnableHostModule host)
            {
                host.ConfigureHostAfterServices(context, builder);
            }
        }

        context.RootModule.ConfigureHostAfterServices(context, builder);
    }

    private void ConfigureServicesInternal(
        StartupContext context,
        IServiceCollection services)
    {
        // 1) Now register services for the specific app type (e.g., Web) which may use context.IsDevelopment.
        ConfigureServicesForAppType(context, services);

        // 2) Let modules register their services next so they can override defaults.
        ConfigureServicesFromModule(context, services);

        // 3) Finally, allow custom registrations from startup to override anything else if needed.
        context.CustomRegistrations?.Invoke(services);
    }

    private void ConfigureServicesFromModule(
        StartupContext context,
        IServiceCollection services)
    {
        foreach (var dep in context.Dependencies.Modules)
        {
            dep.ConfigureServices(context, services);
        }

        context.RootModule.ConfigureServices(context, services);
    }

    protected abstract void ConfigureServicesForAppType(StartupContext context, IServiceCollection services);

    protected virtual IHostBuilder ConfigureBuilderForAppType(StartupContext context, IHostBuilder builder) => builder;
}