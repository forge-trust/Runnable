using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Web;

public record MvcOptions
{
    public static MvcOptions Default => new();

    public MvcSupport MvcSupportLevel { get; set; } = MvcSupport.Controllers;

    public Action<IMvcBuilder>? ConfigureMvc { get; set; }
}

public enum MvcSupport
{
    None,
    Controllers,
    ControllersWithViews,
    Full
}
