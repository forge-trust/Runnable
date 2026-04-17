namespace ForgeTrust.Runnable.Web;

/// <summary>
/// Controls whether Runnable's conventional browser-friendly 404 page is automatically enabled.
/// </summary>
public enum ConventionalNotFoundPageMode
{
    /// <summary>
    /// Enables the feature automatically for MVC apps that support views.
    /// </summary>
    Auto,

    /// <summary>
    /// Always enables the feature and upgrades MVC support when needed.
    /// </summary>
    Enabled,

    /// <summary>
    /// Always disables the feature.
    /// </summary>
    Disabled
}
