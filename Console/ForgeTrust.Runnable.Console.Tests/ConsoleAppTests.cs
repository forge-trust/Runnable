using System;
using System.Threading;
using ForgeTrust.Runnable.Console;
using ForgeTrust.Runnable.Console.Tests;
using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class ConsoleAppTests
{
    [Fact]
    public async Task RunAsync_WithCustomStartup_UsesStartupAndRunsHost()
    {
        TrackingStartup.Reset();
        TrackingModule.Reset();

        var previousExitCode = Environment.ExitCode;
        await ConsoleApp<TrackingStartup, TrackingModule>.RunAsync([]);

        Assert.Equal(previousExitCode, Environment.ExitCode);
        Assert.Equal(1, TrackingStartup.InstancesCreated);
        Assert.True(TrackingStartup.HostStarted);
        Assert.Equal(1, TrackingModule.ConfigureServicesCalls);
    }

    [Fact]
    public async Task RunAsync_WithModuleOnly_UsesGenericStartup()
    {
        TrackingModule.Reset();

        var previousExitCode = Environment.ExitCode;
        await ConsoleApp<TrackingModule>.RunAsync([]);

        Assert.Equal(previousExitCode, Environment.ExitCode);
        Assert.Equal(1, TrackingModule.ConfigureServicesCalls);
    }

    private class TrackingStartup : ConsoleStartup<TrackingModule>
    {
        public static int InstancesCreated { get; private set; }
        public static bool HostStarted { get; private set; }

        public TrackingStartup()
        {
            InstancesCreated++;
        }

        protected override void ConfigureAdditionalServices(StartupContext context, IServiceCollection services)
        {
            services.AddSingleton<IHostedService>(sp =>
                new CallbackHostedService(() => HostStarted = true, sp.GetRequiredService<IHostApplicationLifetime>()));
        }

        public static void Reset()
        {
            InstancesCreated = 0;
            HostStarted = false;
        }
    }

    private class TrackingModule : IRunnableHostModule
    {
        public static int ConfigureServicesCalls { get; private set; }

        public void ConfigureServices(StartupContext context, IServiceCollection services)
        {
            ConfigureServicesCalls++;
            services.AddSingleton<IHostedService>(sp =>
                new CallbackHostedService(() => { }, sp.GetRequiredService<IHostApplicationLifetime>()));
        }

        public void RegisterDependentModules(ModuleDependencyBuilder builder)
        {
            builder.AddModule<SharedTestCommandsModule>();
        }

        public void ConfigureHostBeforeServices(StartupContext context, IHostBuilder builder)
        {
        }

        public void ConfigureHostAfterServices(StartupContext context, IHostBuilder builder)
        {
        }

        public static void Reset() => ConfigureServicesCalls = 0;
    }

    private class CallbackHostedService : IHostedService
    {
        private readonly Action _callback;
        private readonly IHostApplicationLifetime _lifetime;

        public CallbackHostedService(Action callback, IHostApplicationLifetime lifetime)
        {
            _callback = callback;
            _lifetime = lifetime;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _callback();
            _lifetime.StopApplication();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
