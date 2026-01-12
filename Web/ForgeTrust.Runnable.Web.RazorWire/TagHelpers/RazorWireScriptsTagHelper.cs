using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ForgeTrust.Runnable.Web.RazorWire.TagHelpers;

/// <summary>
/// Tag helper for rendering the necessary RazorWire scripts.
/// </summary>
[HtmlTargetElement("rw:scripts")]
public class RazorWireScriptsTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null; // No wrapper tag
        
        // This includes Turbo.js and the custom RazorWire island loader.
        output.Content.SetHtmlContent(@"
<script src=""/_content/ForgeTrust.Runnable.Web.RazorWire/razorwire/turbo.min.js""></script>
<script src=""/_content/ForgeTrust.Runnable.Web.RazorWire/razorwire/razorwire.islands.js""></script>
");
    }
}
