using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Web.RazorWire.TagHelpers;

[HtmlTargetElement("rw:stream-source")]
public class StreamSourceTagHelper : TagHelper
{
    public string Channel { get; set; } = null!;

    private readonly RazorWireOptions _options;

    public StreamSourceTagHelper(RazorWireOptions options)
    {
        _options = options;
    }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "turbo-stream-source";
        output.TagMode = TagMode.StartTagAndEndTag;

        var src = $"{_options.Streams.BasePath}/{Channel}";
        output.Attributes.SetAttribute("src", src);
    }
}
