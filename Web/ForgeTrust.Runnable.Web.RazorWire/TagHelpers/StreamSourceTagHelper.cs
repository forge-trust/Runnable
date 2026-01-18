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
