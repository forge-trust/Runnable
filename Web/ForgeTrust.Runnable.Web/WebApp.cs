using Microsoft.AspNetCore.Routing;

namespace ForgeTrust.Runnable.Web;

public static class WebApp<TStartup, TModule>
    where TStartup : WebStartup<TModule>, new()
    where TModule : IRunnableWebModule, new()
{
    public static Task RunAsync(
        string[] args,
        Action<IEndpointRouteBuilder>? mapEndpoints = null) =>
        new TStartup()
            .WithDirectConfiguration(mapEndpoints)
            .RunAsync(args);
}

public static class WebApp<TModule>
    where TModule : IRunnableWebModule, new()
{
    public static Task RunAsync(
        string[] args,
        Action<IEndpointRouteBuilder>? mapEndpoints = null) =>
        WebApp<GenericWebStartup<TModule>, TModule>.RunAsync(args, mapEndpoints);

    private class GenericWebStartup<TNew> : WebStartup<TNew>
        where TNew : IRunnableWebModule, new()
    {
    }
}
