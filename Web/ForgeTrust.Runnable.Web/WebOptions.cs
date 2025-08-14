namespace ForgeTrust.Runnable.Web;

using Microsoft.AspNetCore.Routing;

public record WebOptions
{
    public static WebOptions Default => new();

    public CorsOptions Cors { get; set; } = CorsOptions.Default;

    public Action<IEndpointRouteBuilder> MapEndpoints { get; set; } = _ =>
    {
        // Default endpoint mapping can be empty or provide a basic route
    };
}
