using ForgeTrust.Runnable.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace ForgeTrust.Runnable.Web;

public interface IRunnableWebModule : IRunnableHostModule
{
    void ConfigureEndpoints(StartupContext context, IEndpointRouteBuilder endpoints)
    {
        // Default implementation does nothing, so we don't force an implementation.
    }
    
    void ConfigureWebApplication(StartupContext context, IApplicationBuilder app);
}
