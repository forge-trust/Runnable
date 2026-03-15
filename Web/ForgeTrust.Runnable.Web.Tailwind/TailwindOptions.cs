namespace ForgeTrust.Runnable.Web.Tailwind;

/// <summary>
/// Options for rendering a project's compiled Tailwind stylesheet.
/// </summary>
public class TailwindOptions
{
    /// <summary>
    /// Gets or sets the application-relative path for the compiled stylesheet.
    /// </summary>
    public string StylesheetPath { get; set; } = "~/css/site.css";
}
