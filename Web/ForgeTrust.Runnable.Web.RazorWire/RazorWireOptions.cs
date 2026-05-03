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

    /// <summary>
    /// Gets configuration options for RazorWire-enhanced form submissions.
    /// </summary>
    public RazorWireFormOptions Forms { get; } = new();
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

/// <summary>
/// Represents configuration options for failed <c>rw-active</c> form submissions.
/// </summary>
public class RazorWireFormOptions
{
    private const string DefaultFailureMessageFallback = "We could not submit this form. Check your input and try again.";
    private string _defaultFailureMessage = DefaultFailureMessageFallback;

    /// <summary>
    /// Gets or sets a value indicating whether RazorWire emits failed-form request markers,
    /// lifecycle hooks, and default failure behavior.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool EnableFailureUx { get; set; } = true;

    /// <summary>
    /// Gets or sets the package-level failed-form behavior.
    /// Defaults to <see cref="RazorWireFormFailureMode.Auto"/>.
    /// </summary>
    public RazorWireFormFailureMode FailureMode { get; set; } = RazorWireFormFailureMode.Auto;

    /// <summary>
    /// Gets or sets a value indicating whether development-only diagnostics may be shown.
    /// Defaults to <c>true</c>; diagnostics are still emitted only when the host is running in Development.
    /// </summary>
    public bool EnableDevelopmentDiagnostics { get; set; } = true;

    /// <summary>
    /// Gets or sets the safe default message used for generic failed form submissions.
    /// </summary>
    public string DefaultFailureMessage
    {
        get => _defaultFailureMessage;
        set => _defaultFailureMessage = string.IsNullOrWhiteSpace(value)
            ? DefaultFailureMessageFallback
            : value;
    }
}

/// <summary>
/// Defines the package-level failed-form behavior for <c>rw-active</c> forms.
/// </summary>
public enum RazorWireFormFailureMode
{
    /// <summary>
    /// Render RazorWire's default form-local fallback UI when the server did not handle the failure.
    /// </summary>
    Auto,

    /// <summary>
    /// Emit request markers and lifecycle events, but do not render fallback UI.
    /// </summary>
    Manual,

    /// <summary>
    /// Disable RazorWire's failed-form request markers, events, and fallback UI by default.
    /// </summary>
    Off
}
