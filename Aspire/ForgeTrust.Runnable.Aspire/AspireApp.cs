using System.Reflection;
using ForgeTrust.Runnable.Core;
using ForgeTrust.Runnable.Core.Defaults;

namespace ForgeTrust.Runnable.Aspire;

public static class AspireApp
{
    public static Task RunAsync(string[] args)
    {
        var startupContext = new StartupContext(args, new NoHostModule())
        {
            OverrideEntryPointAssembly = Assembly.GetCallingAssembly()
        };

        return new AspireAppStartup<NoHostModule>().RunAsync(startupContext);
    }
}

public static class AspireApp<TModule>
    where TModule : IRunnableHostModule, new()
{
    public static Task RunAsync(string[] args) => new AspireAppStartup<TModule>()
        .RunAsync(args);

}
