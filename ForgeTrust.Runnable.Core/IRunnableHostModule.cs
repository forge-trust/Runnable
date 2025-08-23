using Microsoft.Extensions.Hosting;

namespace ForgeTrust.Runnable.Core;

public interface IRunnableHostModule : IRunnableModule
{
    void ConfigureHostBeforeServices(StartupContext context, IHostBuilder builder);

    void ConfigureHostAfterServices(StartupContext context, IHostBuilder builder);
}
