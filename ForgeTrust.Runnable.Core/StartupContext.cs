using System.Reflection;
using ForgeTrust.Runnable.Core.Defaults;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Core;

/// <summary>
/// Provides context and configuration during the application startup process.
/// </summary>
/// <param name="Args">Command-line arguments provided to the application.</param>
/// <param name="RootModule">The root module of the application.</param>
/// <param name="ApplicationName">Optional name of the application.</param>
/// <param name="EnvironmentProvider">Optional provider for environment information.</param>
/// <param name="CustomRegistrations">Optional custom service registrations.</param>
public record StartupContext(
    string[] Args,
    IRunnableHostModule RootModule,
    string? ApplicationName = null,
    IEnvironmentProvider? EnvironmentProvider = null,
    params List<Action<IServiceCollection>> CustomRegistrations)
{
    internal ModuleDependencyBuilder Dependencies { get; } = new();

    /// <summary>
    /// Gets or sets an assembly that should be treated as the entry point assembly, overriding the default.
    /// </summary>
    public Assembly? OverrideEntryPointAssembly { get; set; } = null;

    /// <summary>
    /// Gets the assembly that contains the root module.
    /// </summary>
    public Assembly RootModuleAssembly { get; } = RootModule.GetType().Assembly;

    /// <summary>
    /// Gets the entry point assembly for the application.
    /// </summary>
    public Assembly EntryPointAssembly => OverrideEntryPointAssembly ?? RootModuleAssembly;

    /// <summary>
    /// Gets the environment provider for the application.
    /// </summary>
    public IEnvironmentProvider EnvironmentProvider { get; } = EnvironmentProvider ?? new DefaultEnvironmentProvider();

    /// <summary>
    /// Gets a value indicating whether the current environment is development.
    /// </summary>
    public bool IsDevelopment => EnvironmentProvider.IsDevelopment;

    /// <summary>
    /// Gets the name of the application.
    /// </summary>
    public string ApplicationName { get; } =
        ApplicationName ?? RootModule.GetType().Assembly.GetName().Name ?? "RunnableApp";

    /// <summary>
    /// Gets the list of modules that the application depends on.
    /// </summary>
    /// <returns>A read-only list of dependent modules.</returns>
    public IReadOnlyList<IRunnableModule> GetDependencies() =>
        Dependencies.Modules
            .ToList();
}
