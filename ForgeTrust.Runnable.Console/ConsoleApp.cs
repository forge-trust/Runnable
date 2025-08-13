using ForgeTrust.Runnable.Core;

namespace ForgeTrust.Runnable.Console;

/// <summary>
/// This class is used to run a console application with a specified startup class and module.
/// This allows for further customization of the consoles startup process.
/// </summary>
public static class ConsoleApp<TStartup, TModule>
    where TStartup : ConsoleStartup<TModule>, new()
    where TModule : IRunnableHostModule, new()
{
    public static Task RunAsync(string [] args) => new TStartup()
        .RunAsync(args);
}

public static class ConsoleApp<TModule>
    where TModule : IRunnableHostModule, new()
{
    public static Task RunAsync(string[] args) => new GenericConsoleStartup()
        .RunAsync(args);
    
    private class GenericConsoleStartup : ConsoleStartup<TModule>
    {
        
    }
}