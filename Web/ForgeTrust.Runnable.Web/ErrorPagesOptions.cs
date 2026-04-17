namespace ForgeTrust.Runnable.Web;

/// <summary>
/// Represents configuration options for Runnable's conventional browser error pages.
/// </summary>
/// <remarks>
/// The default configuration keeps <see cref="NotFoundPageMode"/> at <see cref="ConventionalNotFoundPageMode.Auto"/>,
/// which enables the conventional page only when MVC support already includes Razor views. Apps that need the
/// HTML 404 experience regardless of their starting MVC mode can opt into
/// <see cref="ConventionalNotFoundPageMode.Enabled"/>, while API-only or custom error handling stacks should use
/// <see cref="ConventionalNotFoundPageMode.Disabled"/>.
/// </remarks>
public record ErrorPagesOptions
{
    /// <summary>
    /// Gets a default instance of <see cref="ErrorPagesOptions"/> with <see cref="ConventionalNotFoundPageMode.Auto"/>.
    /// </summary>
    public static ErrorPagesOptions Default => new();

    /// <summary>
    /// Gets or sets the conventional not-found page behavior for the application.
    /// </summary>
    /// <remarks>
    /// <see cref="ConventionalNotFoundPageMode.Auto"/> is the default and turns the feature on only when the
    /// app's MVC support reaches <see cref="MvcSupport.ControllersWithViews"/>. Choosing
    /// <see cref="ConventionalNotFoundPageMode.Enabled"/> can cause Runnable startup to upgrade MVC support so
    /// the conventional Razor view can render. Choosing <see cref="ConventionalNotFoundPageMode.Disabled"/>
    /// prevents the reserved framework route and browser-oriented 404 handling from activating.
    /// </remarks>
    public ConventionalNotFoundPageMode NotFoundPageMode { get; set; } = ConventionalNotFoundPageMode.Auto;

    /// <summary>
    /// Explicitly enables Runnable's conventional not-found page.
    /// </summary>
    /// <remarks>
    /// Use this when an app must always render the conventional HTML 404 page. Runnable may effectively require
    /// controllers with views at startup so the configured Razor page can execute.
    /// </remarks>
    public void UseConventionalNotFoundPage()
    {
        NotFoundPageMode = ConventionalNotFoundPageMode.Enabled;
    }

    /// <summary>
    /// Explicitly disables Runnable's conventional not-found page.
    /// </summary>
    /// <remarks>
    /// Use this for APIs, custom status-code middleware, or any app that wants to keep the conventional 404
    /// route and browser handling out of the pipeline even when MVC view support is available.
    /// </remarks>
    public void DisableNotFoundPage()
    {
        NotFoundPageMode = ConventionalNotFoundPageMode.Disabled;
    }

    /// <summary>
    /// Determines whether Runnable should enable the conventional not-found page for the supplied MVC support level.
    /// </summary>
    /// <param name="mvcSupportLevel">The MVC capability currently configured for the app.</param>
    /// <returns>
    /// <see langword="true"/> when the conventional page should be active; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This helper is used by Runnable startup after module and app options are applied. In
    /// <see cref="ConventionalNotFoundPageMode.Auto"/>, the feature only turns on when MVC already includes
    /// views. In <see cref="ConventionalNotFoundPageMode.Enabled"/>, the feature is active regardless of the
    /// incoming MVC level because startup may upgrade the app to support Razor views.
    /// </remarks>
    internal bool IsConventionalNotFoundPageEnabled(MvcSupport mvcSupportLevel)
    {
        return NotFoundPageMode switch
        {
            ConventionalNotFoundPageMode.Enabled => true,
            ConventionalNotFoundPageMode.Disabled => false,
            _ => mvcSupportLevel >= MvcSupport.ControllersWithViews
        };
    }
}
