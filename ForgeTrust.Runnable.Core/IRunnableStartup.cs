using Microsoft.Extensions.Hosting;

namespace ForgeTrust.Runnable.Core;

public interface IRunnableStartup
{
    IHostBuilder CreateHostBuilder(StartupContext context);
    Task RunAsync(StartupContext context);
}
