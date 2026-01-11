using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ForgeTrust.Runnable.Web.RazorWire.TagHelpers;

[HtmlTargetElement("rw:turbo-scripts")]
public class TurboScriptsTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null; // No wrapper tag

        output.Content.AppendHtml("<script src=\"/_content/ForgeTrust.Runnable.Web.RazorWire/razorwire/turbo.min.js\" type=\"module\"></script>\n");
        output.Content.AppendHtml("<script src=\"/_content/ForgeTrust.Runnable.Web.RazorWire/razorwire/razorwire.islands.js\" type=\"module\"></script>");
    }
}
