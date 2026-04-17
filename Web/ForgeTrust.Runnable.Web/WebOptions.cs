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
    /// Gets or sets configuration options for conventional framework error pages.
    /// </summary>
    /// <remarks>
    /// The default value is <see cref="ErrorPagesOptions.Default"/>, which leaves the not-found mode in
    /// <see cref="ConventionalNotFoundPageMode.Auto"/>. In that mode, Runnable only enables the conventional
    /// browser 404 experience when MVC support already includes views. Use explicit modes when an app must
    /// always force or always suppress the conventional page. When enabled, Runnable reserves
    /// <c>/_runnable/errors/404</c> for direct rendering and export tooling, and ignores that path when deciding
    /// whether to apply browser-oriented status-page middleware.
    /// </remarks>
    public ErrorPagesOptions Errors { get; set; } = ErrorPagesOptions.Default;

    /// <summary>
    /// Gets or sets an optional delegate to configure endpoint routing for the application.
    /// </summary>
    public Action<IEndpointRouteBuilder>? MapEndpoints { get; set; }
}
