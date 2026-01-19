using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ForgeTrust.Runnable.Web.RazorWire.TagHelpers;

[HtmlTargetElement(Attributes = "requires-stream")]
public class RequiresStreamTagHelper : TagHelper
{
    [HtmlAttributeName("requires-stream")] public string? RequiresStream { get; set; }

    /// <summary>
    /// Applies stream-requirement attributes and disables the element when a requires-stream value is present.
    /// </summary>
    /// <remarks>
    /// If <see cref="RequiresStream"/> is null or empty, the output is left unchanged. Otherwise adds the following attributes to the element:
    /// <list type="bullet">
    /// <item><description><c>data-rw-requires-stream</c> set to the <see cref="RequiresStream"/> value</description></item>
    /// <item><description><c>aria-disabled</c> to <c>"true"</c></description></item>
    /// <item><description><c>disabled</c> to <c>"disabled"</c></description></item>
    /// </list>
    /// The element is therefore disabled until client-side code removes or updates these attributes.
    /// </remarks>
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