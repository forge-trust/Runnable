namespace ForgeTrust.Runnable.Web.Tailwind;

/// <summary>
/// Configuration options for the Tailwind CSS integration.
/// </summary>
public class TailwindOptions
{
    /// <summary>
    /// Gets or sets whether the Tailwind CSS integration is enabled.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the path to the input CSS file.
    /// Defaults to <c>"wwwroot/css/app.css"</c>.
    /// </summary>
    public string InputPath { get; set; } = "wwwroot/css/app.css";

    /// <summary>
    /// Gets or sets the path where the generated CSS file should be written.
    /// Defaults to <c>"wwwroot/css/site.css"</c>.
    /// </summary>
    public string OutputPath { get; set; } = "wwwroot/css/site.css";
}
