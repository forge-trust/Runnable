using Microsoft.Extensions.Hosting;

namespace ForgeTrust.Runnable.Core;

/// <summary>
/// Defines the entry point for starting and running a Runnable application.
/// </summary>
public interface IRunnableStartup
{
    /// <summary>
    /// Creates and configures the <see cref="IHostBuilder"/> for the application.
    /// </summary>
    /// <param name="context">The startup context containing configuration and arguments.</param>
    /// <returns>A configured host builder.</returns>
    IHostBuilder CreateHostBuilder(StartupContext context);

    /// <summary>
    /// Runs the application asynchronously using the provided startup context.
    /// </summary>
    /// <param name="context">The startup context for the application.</param>
    /// <returns>A task that represents the asynchronous run operation.</returns>
    Task RunAsync(StartupContext context);
}
