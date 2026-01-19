using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Web.RazorWire.TagHelpers;

[HtmlTargetElement("rw:stream-source")]
public class StreamSourceTagHelper : TagHelper
{
    public string Channel { get; set; } = string.Empty;

    public bool Permanent { get; set; }

    private readonly RazorWireOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="StreamSourceTagHelper"/> with the specified RazorWire options.
    /// </summary>
    /// <param name="options">Configuration options used to build stream source URLs.</param>
    public StreamSourceTagHelper(RazorWireOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Renders an rw-stream-source element and sets its attributes from the tag helper's properties.
    /// </summary>
    /// <remarks>
    /// Sets the tag name to "rw-stream-source" and its mode to StartTagAndEndTag. Validates that <see cref="Channel"/> is not null, empty, or whitespace; sets the "src" attribute to the configured stream base path combined with <see cref="Channel"/>. If <see cref="Permanent"/> is true, adds the "data-turbo-permanent" attribute with an empty value.
    /// </remarks>
    /// <summary>
    /// Renders an rw-stream-source element and sets its attributes based on the tag helper configuration.
    /// </summary>
    /// <remarks>
    /// Validates that <see cref="Channel"/> is provided, sets the element's <c>src</c> attribute to the configured streams base path combined with <see cref="Channel"/>, and adds a <c>data-turbo-permanent</c> attribute when <see cref="Permanent"/> is true.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when <see cref="Channel"/> is null, empty, or contains only whitespace.</exception>
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "rw-stream-source";
        output.TagMode = TagMode.StartTagAndEndTag;

        if (string.IsNullOrWhiteSpace(Channel))
        {
            throw new InvalidOperationException(
                $"The 'channel' attribute is required for the 'rw:stream-source' tag helper.");
        }

        var src = $"{_options.Streams.BasePath}/{Channel}";
        output.Attributes.SetAttribute("src", src);

        if (Permanent)
        {
            output.Attributes.SetAttribute("data-turbo-permanent", "");
        }
    }
}