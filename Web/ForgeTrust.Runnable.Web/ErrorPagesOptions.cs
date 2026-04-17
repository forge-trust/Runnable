namespace ForgeTrust.Runnable.Web;

/// <summary>
/// Represents configuration options for Runnable's conventional browser error pages.
/// </summary>
public record ErrorPagesOptions
{
    /// <summary>
    /// Gets a default instance of <see cref="ErrorPagesOptions"/>.
    /// </summary>
    public static ErrorPagesOptions Default => new();

    /// <summary>
    /// Gets or sets the conventional not-found page behavior for the application.
    /// </summary>
    public ConventionalNotFoundPageMode NotFoundPageMode { get; set; } = ConventionalNotFoundPageMode.Auto;

    /// <summary>
    /// Explicitly enables Runnable's conventional not-found page.
    /// </summary>
    public void UseConventionalNotFoundPage()
    {
        NotFoundPageMode = ConventionalNotFoundPageMode.Enabled;
    }

    /// <summary>
    /// Explicitly disables Runnable's conventional not-found page.
    /// </summary>
    public void DisableNotFoundPage()
    {
        NotFoundPageMode = ConventionalNotFoundPageMode.Disabled;
    }

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
