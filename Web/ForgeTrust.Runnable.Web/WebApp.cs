namespace ForgeTrust.Runnable.Web;

public static class WebApp<TStartup, TModule>
    where TStartup : WebStartup<TModule>, new()
    where TModule : IRunnableWebModule, new()
{
    public static Task RunAsync(
        string[] args,
        Action<WebOptions>? configureOptions = null) =>
        new TStartup()
            .WithOptions(configureOptions)
            .RunAsync(args);
}

public static class WebApp<TModule>
    where TModule : IRunnableWebModule, new()
{
    public static Task RunAsync(
        string[] args,
        Action<WebOptions>? configureOptions = null) =>
        WebApp<GenericWebStartup<TModule>, TModule>.RunAsync(args, configureOptions);

    private class GenericWebStartup<TNew> : WebStartup<TNew>
        where TNew : IRunnableWebModule, new()
    {
    }
}
