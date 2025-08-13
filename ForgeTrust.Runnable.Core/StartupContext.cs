using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Core;

public record StartupContext(
    string[] Args,
    IRunnableHostModule RootModule,
    string? ApplicationName = null,
    Action<IServiceCollection>? CustomRegistrations = null)
{
   internal ModuleDependencyBuilder Dependencies { get; } = new ModuleDependencyBuilder();

   public Assembly EntryPointAssembly { get; } = RootModule.GetType().Assembly;
   
   public string ApplicationName { get; } = ApplicationName ?? RootModule.GetType().Assembly.GetName().Name ?? "RunnableApp";
   
   public IReadOnlyList<IRunnableModule> GetDependencies()
   {
       return Dependencies.Modules
           .ToList();
   }
}
