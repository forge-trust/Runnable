namespace ForgeTrust.Runnable.Web;

using Microsoft.AspNetCore.Routing;

/// <summary>
/// Represents configuration options for the web application, including MVC, CORS, and static file settings.
/// </summary>
public record WebOptions
{
    /// <summary>
    /// Gets a default instance of <see cref="WebOptions"/> with default configuration settings.
    /// </summary>
    public static readonly WebOptions Default = new();

    /// <summary>
    /// Gets or sets MVC-specific configuration options, such as support levels and custom MVC configuration.
    /// </summary>
    public MvcOptions Mvc { get; set; } = MvcOptions.Default;

    /// <summary>
    /// Gets or sets CORS configuration options for defining cross-origin resource sharing policies.
    /// </summary>
    public CorsOptions Cors { get; set; } = CorsOptions.Default;

    /// <summary>
    /// Gets or sets configuration options for serving static files within the web application.
    /// </summary>
    public StaticFilesOptions StaticFiles { get; set; } = StaticFilesOptions.Default;

    /// <summary>
    /// Gets or sets an optional delegate to configure endpoint routing for the application.
    /// </summary>
    public Action<IEndpointRouteBuilder>? MapEndpoints { get; set; }
}
