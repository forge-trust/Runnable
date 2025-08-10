using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Web;

public static class WebApp<TStartup, TModule>
    where TStartup : WebStartup<TModule>, new()
    where TModule : IRunnableHostModule, new()
{
    public static Task RunAsync(string[] args) => new TStartup().RunAsync(args);
}

public class WebApp<TModule>
    where TModule : IRunnableHostModule, new()
{
    public static Task RunAsync(string[] args) => new GenericWebStartup().RunAsync(args);

    private class GenericWebStartup : WebStartup<TModule>
    {
    }
}
