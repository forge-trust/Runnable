using System.Reflection;
using ForgeTrust.Runnable.Core.Defaults;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Core;

public record StartupContext(
    string[] Args,
    IRunnableHostModule RootModule,
    string? ApplicationName = null,
    IEnvironmentProvider? EnvironmentProvider = null,
    params List<Action<IServiceCollection>> CustomRegistrations)
{
    internal ModuleDependencyBuilder Dependencies { get; } = new();

    public Assembly? OverrideEntryPointAssembly { get; set; } = null;

    public Assembly RootModuleAssembly { get; } = RootModule.GetType().Assembly;

    public Assembly EntryPointAssembly => OverrideEntryPointAssembly ?? RootModuleAssembly;

    public IEnvironmentProvider EnvironmentProvider { get; } = EnvironmentProvider ?? new DefaultEnvironmentProvider();

    public bool IsDevelopment => EnvironmentProvider.IsDevelopment;

    public string ApplicationName { get; } =
        ApplicationName ?? RootModule.GetType().Assembly.GetName().Name ?? "RunnableApp";

    public IReadOnlyList<IRunnableModule> GetDependencies() =>
        Dependencies.Modules
            .ToList();
}
