namespace ForgeTrust.Runnable.Web;

using Microsoft.AspNetCore.Routing;

public record WebOptions
{
    public static readonly WebOptions Default = new();

    public MvcOptions Mvc { get; set; } = MvcOptions.Default;

    public CorsOptions Cors { get; set; } = CorsOptions.Default;

    public Action<IEndpointRouteBuilder>? MapEndpoints { get; set; }
}
