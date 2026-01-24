using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Console;

/// <summary>
///     This class is used to run a console application with a specified startup class and module.
///     This allows for further customization of the consoles startup process.
/// </summary>
public static class ConsoleApp<TStartup, TModule>
    where TStartup : ConsoleStartup<TModule>, new()
    where TModule : IRunnableHostModule, new()
{
    /// <summary>
    /// Runs the console application asynchronously using a custom startup class.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A task representing the run operation.</returns>
    public static Task RunAsync(string[] args) =>
        new TStartup()
            .RunAsync(args);
}

/// <summary>
/// This class is used to run a console application with a specified module and a generic startup.
/// </summary>
/// <typeparam name="TModule">The type of the root module.</typeparam>
public static class ConsoleApp<TModule>
    where TModule : IRunnableHostModule, new()
{
    /// <summary>
    /// Runs the console application asynchronously with a generic startup.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A task representing the run operation.</returns>
    public static Task RunAsync(string[] args) =>
        new GenericConsoleStartup()
            .RunAsync(args);

    private class GenericConsoleStartup : ConsoleStartup<TModule>
    {
    }
}
