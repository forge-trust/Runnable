using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Core;

public abstract class RunnableStartup<TRootModule> : IRunnableStartup
    where TRootModule : IRunnableHostModule, new()
{
    public Task RunAsync(string[] args)
    {
        return RunAsync(new StartupContext(args, CreateRootModule()));
    }

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
            CreateBootstrapLogger().LogWarning(ex, "Service(s) did not exit in a timely fashion..");
        }
        catch (Exception e)
        {
            CreateBootstrapLogger().LogCritical(e, "Fatal Processing Error");
            Environment.ExitCode = -100;
        }
    }

    private ILogger CreateBootstrapLogger()
    {
        return LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug))
            .CreateLogger(GetType().Name);
    }

    async Task IRunnableStartup.RunAsync(StartupContext context)
    {
        await RunAsync(context);
    }

    private IHost CreateHost(StartupContext context)
    {
        return ((IRunnableStartup)this).CreateHostBuilder(context).Build();
    }

    IHostBuilder IRunnableStartup.CreateHostBuilder(StartupContext context)
    {
        return CreateHostBuilderCore(context);
    }

    private IHostBuilder CreateHostBuilderCore(StartupContext context)
    {
        var builder = Host.CreateDefaultBuilder();
        
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
        foreach(var dep in context.Dependencies.Modules)
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
        ConfigureServicesForAppType(context, services);
        ConfigureServicesFromModule(context, services);
        context.CustomRegistrations?.Invoke(services);
    }

    private void ConfigureServicesFromModule(
        StartupContext context,
        IServiceCollection services)
    {
        foreach(var dep in context.Dependencies.Modules)
        {
            dep.ConfigureServices(context, services);
        }
        
        context.RootModule.ConfigureServices(context, services);
    }

    protected abstract void ConfigureServicesForAppType(StartupContext context, IServiceCollection services);

    protected virtual IHostBuilder ConfigureBuilderForAppType(StartupContext context, IHostBuilder builder) => builder;
    
}
