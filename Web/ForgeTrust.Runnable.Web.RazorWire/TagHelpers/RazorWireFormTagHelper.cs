using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ForgeTrust.Runnable.Web.RazorWire.TagHelpers;

[HtmlTargetElement("form", Attributes = "rw-active")]
public class RazorWireFormTagHelper : TagHelper
{
    [HtmlAttributeName("rw-active")]
    public bool Enabled { get; set; } = true;

    [HtmlAttributeName("rw-target")]
    public string? TargetFrame { get; set; }


    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!Enabled)
        {
            output.Attributes.SetAttribute("data-turbo", "false");
            return;
        }

        output.Attributes.SetAttribute("data-turbo", "true");

        if (!string.IsNullOrEmpty(TargetFrame))
        {
            output.Attributes.SetAttribute("data-turbo-frame", TargetFrame);
        }
    }
}
