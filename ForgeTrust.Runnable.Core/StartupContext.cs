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

    public Assembly EntryPointAssembly { get; } = RootModule.GetType().Assembly;

    // TODO: Feels odd to be checking ASPNETCORE_ENVIRONMENT,
    // is there a better way we should do this across different hosting types?
    public bool IsDevelopment { get; } = string.Equals(
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
        Environments.Development,
        StringComparison.OrdinalIgnoreCase);

    public string ApplicationName { get; } =
        ApplicationName ?? RootModule.GetType().Assembly.GetName().Name ?? "RunnableApp";

    public IReadOnlyList<IRunnableModule> GetDependencies() =>
        Dependencies.Modules
            .ToList();
}
