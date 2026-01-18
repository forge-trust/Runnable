using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ForgeTrust.Runnable.Web.RazorWire.TagHelpers;

[HtmlTargetElement(Attributes = "requires-stream")]
public class RequiresStreamTagHelper : TagHelper
{
    [HtmlAttributeName("requires-stream")] public string? RequiresStream { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (string.IsNullOrEmpty(RequiresStream))
        {
            return;
        }

        // Add the data attribute for JS to pick up
        output.Attributes.SetAttribute("data-rw-requires-stream", RequiresStream);
        output.Attributes.SetAttribute("aria-disabled", "true");

        // Automatically disable by default until JS enables it
        output.Attributes.SetAttribute("disabled", "disabled");
    }
}
