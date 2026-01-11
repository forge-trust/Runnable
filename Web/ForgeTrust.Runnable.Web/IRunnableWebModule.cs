using ForgeTrust.Runnable.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace ForgeTrust.Runnable.Web;

public interface IRunnableWebModule : IRunnableHostModule
{
    void ConfigureWebOptions(StartupContext context, WebOptions options)
    {
        // Default implementation does nothing, so we don't force an implementation.
    }

    void ConfigureEndpoints(StartupContext context, IEndpointRouteBuilder endpoints)
    {
        // Default implementation does nothing, so we don't force an implementation.
    }

    void ConfigureWebApplication(StartupContext context, IApplicationBuilder app);

    /// <summary>
    /// Gets a value indicating whether this module's assembly should be searched for MVC application parts (controllers, views, etc.).
    /// Defaults to false.
    /// </summary>
    bool IncludeAsApplicationPart => false;
}
