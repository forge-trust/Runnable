using ForgeTrust.Runnable.Core;
using Microsoft.AspNetCore.Builder;

namespace ForgeTrust.Runnable.Web;

public interface IRunnableWebModule : IRunnableHostModule
{
    void ConfigureWebApplication(StartupContext context, IApplicationBuilder app);
}
