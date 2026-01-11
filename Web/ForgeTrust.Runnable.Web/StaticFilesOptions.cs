namespace ForgeTrust.Runnable.Web;

public record StaticFilesOptions
{
    public static readonly StaticFilesOptions Default = new();

    /// <summary>
    /// Gets or sets a value indicating whether static files are enabled.
    /// This is automatically enabled when <see cref="MvcSupport.ControllersWithViews"/> or higher is used.
    /// </summary>
    public bool EnableStaticFiles { get; set; } = false;
}
