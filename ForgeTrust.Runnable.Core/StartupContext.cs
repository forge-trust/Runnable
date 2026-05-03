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
    /// Gets or sets how console-oriented apps should present command output relative to ambient host lifecycle diagnostics.
    /// </summary>
    /// <remarks>
    /// The default value is <see cref="ConsoleOutputMode.Default"/>, which preserves the standard Generic Host behavior.
    /// Console startups can switch to <see cref="ConsoleOutputMode.CommandFirst"/> when command output should remain the
    /// primary user-facing console experience. Non-console apps can ignore this setting.
    /// </remarks>
    public ConsoleOutputMode ConsoleOutputMode { get; set; } = ConsoleOutputMode.Default;

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
    /// Gets the user-facing name of the application.
    /// </summary>
    /// <remarks>
    /// This value is intended for product surfaces such as generated OpenAPI titles, command output, and other places
    /// where callers need a readable application label. It is intentionally separate from <see cref="HostApplicationName"/>,
    /// which must stay aligned with the assembly identity used by the .NET Generic Host and ASP.NET static web asset
    /// manifest discovery.
    /// </remarks>
    public string ApplicationName { get; } =
        ApplicationName ?? RootModule.GetType().Assembly.GetName().Name ?? "RunnableApp";

    /// <summary>
    /// Gets the assembly-backed application identity assigned to <see cref="Microsoft.Extensions.Hosting.IHostEnvironment.ApplicationName"/>.
    /// </summary>
    /// <remarks>
    /// ASP.NET static web assets resolve runtime manifests by host application name. Custom display names should not be
    /// written into the host environment because they can point static web asset discovery at a non-existent manifest.
    /// Override <see cref="OverrideEntryPointAssembly"/> when a test or host needs to select a different manifest identity.
    /// </remarks>
    public string HostApplicationName => EntryPointAssembly.GetName().Name ?? "RunnableApp";

    /// <summary>
    /// Gets the list of modules that the application depends on.
    /// </summary>
    /// <returns>A read-only list of dependent modules.</returns>
    public IReadOnlyList<IRunnableModule> GetDependencies() =>
        Dependencies.Modules
            .ToList();
}
