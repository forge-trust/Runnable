using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Core;

public record StartupContext(
    string[] Args,
    IRunnableHostModule RootModule,
    Action<IServiceCollection>? CustomRegistrations = null)
{
   internal ModuleDependencyBuilder Dependencies { get; } = new ModuleDependencyBuilder();

   public IReadOnlyList<IRunnableModule> GetDependencies()
   {
       return Dependencies.Modules
           .ToList();
   }
}
