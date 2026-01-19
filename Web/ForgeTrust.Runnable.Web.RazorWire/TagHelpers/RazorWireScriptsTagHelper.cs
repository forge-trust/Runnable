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
        output.Content.SetHtmlContent(
            @"
<script src=""https://cdn.jsdelivr.net/npm/@hotwired/turbo@8.0.12/dist/turbo.es2017-umd.js"" integrity=""sha256-1evN/OxCRDJtuVCzQ3gklVq8LzN6qhCm7x/sbawknOk="" crossorigin=""anonymous""></script>
<script src=""/_content/ForgeTrust.Runnable.Web.RazorWire/razorwire/razorwire.js""></script>
<script src=""/_content/ForgeTrust.Runnable.Web.RazorWire/razorwire/razorwire.islands.js""></script>
");
    }
}
