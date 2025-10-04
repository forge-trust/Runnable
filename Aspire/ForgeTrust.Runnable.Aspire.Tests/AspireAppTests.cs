using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using ForgeTrust.Runnable.Aspire;
using ForgeTrust.Runnable.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class AspireAppTests
{
    [Fact]
    public async Task RunAsync_RegistersAspireComponentsAsSingletons()
    {
        var completion = new TaskCompletionSource<ComponentResolutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        TestModule.Completion = completion;

        try
        {
            var runTask = AspireApp<TestModule>.RunAsync(["validate"]);

            var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await runTask.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(result.ComponentResolved);
            Assert.True(result.ResolvedAsSingleton);
        }
        finally
        {
            TestModule.Completion = null;
        }
    }

    private sealed record ComponentResolutionResult(bool ComponentResolved, bool ResolvedAsSingleton);

    [Command("validate")]
    private sealed class ValidateComponentCommand : ICommand
    {
        private readonly IHostApplicationLifetime _lifetime;

        public ValidateComponentCommand(IServiceProvider provider, IHostApplicationLifetime lifetime)
        {
            _lifetime = lifetime;
        }

        public ValueTask ExecuteAsync(IConsole console)
        {
            _lifetime.StopApplication();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestModule : IRunnableHostModule
    {
        public static TaskCompletionSource<ComponentResolutionResult>? Completion { get; set; }

        public void ConfigureHostAfterServices(StartupContext context, IHostBuilder builder)
        {
        }

        public void ConfigureHostBeforeServices(StartupContext context, IHostBuilder builder)
        {
        }

        public void ConfigureServices(StartupContext context, IServiceCollection services)
        {
            var completion = Completion ?? throw new InvalidOperationException("Completion source not initialized.");

            var descriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(TestComponent));
            var registered = descriptor is not null;
            var singleton = descriptor?.Lifetime == ServiceLifetime.Singleton;

            completion.TrySetResult(new ComponentResolutionResult(registered, singleton));
        }

        public void RegisterDependentModules(ModuleDependencyBuilder builder)
        {
        }
    }

    private sealed class TestComponent : IAspireComponent<IResource>
    {
        public IResourceBuilder<IResource> Generate(AspireStartupContext context, IDistributedApplicationBuilder appBuilder) =>
            throw new NotSupportedException("Generate is not expected to be called in this test.");
    }
}
