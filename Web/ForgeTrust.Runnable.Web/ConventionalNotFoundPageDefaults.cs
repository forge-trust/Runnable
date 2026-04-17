namespace ForgeTrust.Runnable.Web;

/// <summary>
/// Defines the conventional paths used by Runnable's built-in browser-friendly 404 handling.
/// </summary>
public static class ConventionalNotFoundPageDefaults
{
    /// <summary>
    /// Gets the conventional application or shared-library override path for the not-found view.
    /// </summary>
    public const string AppViewPath = "~/Views/Shared/404.cshtml";

    /// <summary>
    /// Gets the reserved framework route used to render the not-found page directly.
    /// This route is intended for framework tooling such as static export.
    /// </summary>
    public const string ReservedNotFoundRoute = "/_runnable/errors/404";

    internal const string ReservedRouteFormat = "/_runnable/errors/{0}";
    internal const string ReservedRoutePattern = "/_runnable/errors/{statusCode:int}";
    internal const string FrameworkFallbackViewPath = "~/Views/_Runnable/Errors/404.cshtml";
}
