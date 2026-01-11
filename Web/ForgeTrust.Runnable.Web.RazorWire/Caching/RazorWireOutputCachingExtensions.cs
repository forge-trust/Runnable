using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Web.RazorWire;

public static class RazorWireOutputCachingExtensions
{
    public static OutputCacheOptions AddRazorWirePolicies(this OutputCacheOptions options, RazorWireOptions rwOptions)
    {
        options.AddPolicy(rwOptions.Caching.PagePolicyName, builder =>
        {
            builder.Expire(TimeSpan.FromMinutes(1));
            // Rule: authenticated or personalized content should not be cached unless explicitly configured.
            builder.With(context => context.HttpContext.User.Identity?.IsAuthenticated == false);
        });

        options.AddPolicy(rwOptions.Caching.IslandPolicyName, builder =>
        {
            builder.Expire(TimeSpan.FromSeconds(30));
            builder.With(context => context.HttpContext.User.Identity?.IsAuthenticated == false);
        });

        return options;
    }
}
