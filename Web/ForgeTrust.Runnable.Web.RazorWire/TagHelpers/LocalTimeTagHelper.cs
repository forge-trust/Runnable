using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ForgeTrust.Runnable.Web.RazorWire.TagHelpers;

/// <summary>
/// Tag helper that renders a <c>&lt;time&gt;</c> element with a UTC timestamp for client-side local time formatting.
/// </summary>
/// <remarks>
/// The JavaScript runtime formats the timestamp to the user's local timezone using the browser's <c>Intl.DateTimeFormat</c> API.
/// </remarks>
/// <example>
/// <code>
/// &lt;rw:local-time value="@Model.Timestamp" /&gt;
/// &lt;rw:local-time value="@Model.Timestamp" display="relative" /&gt;
/// &lt;rw:local-time value="@Model.Timestamp" display="datetime" format="short" /&gt;
/// </code>
/// </example>
[HtmlTargetElement("rw:local-time")]
public class LocalTimeTagHelper : TagHelper
{
    /// <summary>
    /// Gets or sets the UTC timestamp to display. This attribute is required.
    /// </summary>
    public DateTimeOffset Value { get; set; }

    /// <summary>
    /// Gets or sets the display mode for the formatted time.
    /// Valid values: <c>time</c> (default), <c>date</c>, <c>datetime</c>, or <c>relative</c>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><c>time</c> - Displays time only (e.g., "3:45 PM")</description></item>
    /// <item><description><c>date</c> - Displays date only (e.g., "Jan 24, 2026")</description></item>
    /// <item><description><c>datetime</c> - Displays both date and time</description></item>
    /// <item><description><c>relative</c> - Displays relative time (e.g., "2 minutes ago")</description></item>
    /// </list>
    /// </remarks>
    public string Display { get; set; } = "time";

    /// <summary>
    /// Gets or sets the format style for absolute time formatting.
    /// Valid values: <c>short</c>, <c>medium</c> (default), <c>long</c>, or <c>full</c>.
    /// </summary>
    /// <remarks>
    /// Maps to the <c>dateStyle</c> and <c>timeStyle</c> options of <c>Intl.DateTimeFormat</c>.
    /// This property is ignored when <see cref="Display"/> is set to <c>relative</c>.
    /// </remarks>
    public string? Format { get; set; }

    /// <summary>
    /// Renders a <c>&lt;time&gt;</c> element with the UTC timestamp in the <c>datetime</c> attribute
    /// and data attributes for client-side JavaScript formatting.
    /// </summary>
    /// <param name="context">Contains information associated with the current HTML tag.</param>
    /// <param name="output">A stateful HTML element used to generate an HTML tag.</param>
    /// <exception cref="ArgumentException">Thrown when <see cref="Value"/> is the default value.</exception>
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (Value == default)
        {
            throw new ArgumentException(
                "The 'value' attribute is required for the 'rw:local-time' tag helper.",
                nameof(Value));
        }

        output.TagName = "time";
        output.TagMode = TagMode.StartTagAndEndTag;

        // Output UTC timestamp in ISO 8601 format
        output.Attributes.SetAttribute("datetime", Value.ToUniversalTime().ToString("o"));

        // Add data attribute to signal JavaScript formatting
        output.Attributes.SetAttribute("data-rw-local-time", "");

        // Add display mode if not default
        if (!string.Equals(Display, "time", StringComparison.OrdinalIgnoreCase))
        {
            output.Attributes.SetAttribute("data-rw-local-time-display", Display.ToLowerInvariant());
        }

        // Add format style if specified
        if (!string.IsNullOrWhiteSpace(Format))
        {
            output.Attributes.SetAttribute("data-rw-local-time-format", Format.ToLowerInvariant());
        }
    }
}
