namespace ForgeTrust.Runnable.Web.RazorWire;

/// <summary>
/// Represents configuration options for the RazorWire real-time streaming and caching system.
/// </summary>
public class RazorWireOptions
{
    /// <summary>
    /// Gets a default instance of <see cref="RazorWireOptions"/> with default configuration settings.
    /// </summary>
    public static RazorWireOptions Default { get; } = new();

    /// <summary>
    /// Gets configuration options for real-time streams, such as the base path for stream connections.
    /// </summary>
    public RazorWireStreamOptions Streams { get; } = new();

    /// <summary>
    /// Gets configuration options for output caching policies used by RazorWire.
    /// </summary>
    public RazorWireCacheOptions Caching { get; } = new();
}

/// <summary>
/// Represents configuration options for RazorWire real-time streams.
/// </summary>
public class RazorWireStreamOptions
{
    /// <summary>
    /// Gets or sets the base path used for establishing stream connections.
    /// Defaults to <c>"/_rw/streams"</c>.
    /// </summary>
    public string BasePath { get; set; } = "/_rw/streams";
}

/// <summary>
/// Represents configuration options for RazorWire output caching.
/// </summary>
public class RazorWireCacheOptions
{
    /// <summary>
    /// Gets or sets the name of the output cache policy for full pages.
    /// Defaults to <c>"rw-page"</c>.
    /// </summary>
    public string PagePolicyName { get; set; } = "rw-page";

    /// <summary>
    /// Gets or sets the name of the output cache policy for individual islands.
    /// Defaults to <c>"rw-island"</c>.
    /// </summary>
    public string IslandPolicyName { get; set; } = "rw-island";
}
