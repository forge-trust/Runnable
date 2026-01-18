using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Web.RazorWire.TagHelpers;

[HtmlTargetElement("rw:stream-source")]
public class StreamSourceTagHelper : TagHelper
{
    public string Channel { get; set; } = string.Empty;

    public bool Permanent { get; set; }

    private readonly RazorWireOptions _options;

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