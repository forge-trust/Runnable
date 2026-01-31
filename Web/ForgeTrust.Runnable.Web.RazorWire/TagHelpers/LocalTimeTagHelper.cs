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
/// &lt;time rw-type="local" datetime="@Model.Timestamp"&gt;@Model.Timestamp.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss UTC")&lt;/time&gt;
/// &lt;time rw-type="local" datetime="@Model.Timestamp" rw-display="relative"&gt;@Model.Timestamp.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss UTC")&lt;/time&gt;
/// &lt;time rw-type="local" datetime="@Model.Timestamp" rw-display="datetime" rw-format="short"&gt;@Model.Timestamp.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss UTC")&lt;/time&gt;
/// </code>
/// </example>
[HtmlTargetElement("time", Attributes = "rw-type")]
public class LocalTimeTagHelper : TagHelper
{
    /// <summary>
    /// Gets or sets the trigger type for RazorWire time formatting.
    /// Must be set to <c>local</c> for this helper to process the element.
    /// </summary>
    [HtmlAttributeName("rw-type")]
    public string? RwType { get; set; }

    /// <summary>
    /// The UTC timestamp to display, sourced from the <c>datetime</c> attribute.
    /// </summary>
    [HtmlAttributeName("datetime")]
    public DateTimeOffset Value { get; set; }

    /// <summary>
    /// Gets or sets the display mode for the formatted time.
    /// Valid values: <c>time</c> (default), <c>date</c>, <c>datetime</c>, or <c>relative</c>.
    /// </summary>
    [HtmlAttributeName("rw-display")]
    public string Display { get; set; } = "time";

    /// <summary>
    /// Gets or sets the format style for absolute time formatting.
    /// Valid values: <c>short</c>, <c>medium</c> (default), <c>long</c>, or <c>full</c>.
    /// </summary>
    [HtmlAttributeName("rw-format")]
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
        if (!string.Equals(RwType, "local", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (Value == default)
        {
            throw new ArgumentException(
                "The 'datetime' attribute is required for the local time tag helper.",
                nameof(Value));
        }

        output.TagMode = TagMode.StartTagAndEndTag;
        output.Content.SetContent(Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss UTC"));

        // Ensure datetime attribute is in ISO 8601 format
        output.Attributes.SetAttribute("datetime", Value.ToUniversalTime().ToString("o"));

        // Add data attribute to signal JavaScript formatting
        output.Attributes.SetAttribute("data-rw-time", "");

        // Add display mode if not default
        if (!string.Equals(Display, "time", StringComparison.OrdinalIgnoreCase))
        {
            output.Attributes.SetAttribute("data-rw-time-display", Display.ToLowerInvariant());
        }

        // Add format style if specified
        if (!string.IsNullOrWhiteSpace(Format))
        {
            output.Attributes.SetAttribute("data-rw-time-format", Format.ToLowerInvariant());
        }

        // Remove the trigger attribute
        output.Attributes.RemoveAll("rw-type");
    }
}
