using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ForgeTrust.Runnable.Core;

public record StartupContext(
    string[] Args,
    IRunnableHostModule RootModule,
    string? ApplicationName = null,
    Action<IServiceCollection>? CustomRegistrations = null)
{
    internal ModuleDependencyBuilder Dependencies { get; } = new();

    public Assembly? OverrideEntryPointAssembly { get; set; } = null;

    public Assembly RootModuleAssembly { get; } = RootModule.GetType().Assembly;

    public Assembly EntryPointAssembly => OverrideEntryPointAssembly ?? RootModuleAssembly;

    /// <summary>
    /// This property is set internally by the Runnable host to provide
    /// access to environment information, such as whether the application
    /// is running in a development environment. It can be overridden by
    /// registering a custom implementation of IEnvironmentProvider.
    /// </summary>
    public IEnvironmentProvider? EnvironmentProvider { get; internal set; } = null;

    public bool IsDevelopment => EnvironmentProvider?.IsDevelopment ?? false;

    public string ApplicationName { get; } =
        ApplicationName ?? RootModule.GetType().Assembly.GetName().Name ?? "RunnableApp";

    public IReadOnlyList<IRunnableModule> GetDependencies() =>
        Dependencies.Modules
            .ToList();
}
