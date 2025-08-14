namespace ForgeTrust.Runnable.Web;

using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Routing;

public record WebOptions
{
    public static WebOptions Default => new();

    public OpenApiOptions OpenApi { get; set; } = OpenApiOptions.Default;

    public CorsOptions Cors { get; set; } = CorsOptions.Default;

    public Action<IEndpointRouteBuilder>? MapEndpoints { get; set; }
        = null;
}

public record OpenApiOptions(bool EnableOpenApi = true, bool EnableSwaggerUi = true)
{
    public static OpenApiOptions Default => new();
}

public record CorsOptions(
    bool EnableCors = false,
    string PolicyName = "CorsPolicy",
    Action<CorsPolicyBuilder>? ConfigurePolicy = null)
{
    public static CorsOptions Default => new();
}
