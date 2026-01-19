using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ForgeTrust.Runnable.Web.RazorWire.TagHelpers;

[HtmlTargetElement(Attributes = "requires-stream")]
public class RequiresStreamTagHelper : TagHelper
{
    [HtmlAttributeName("requires-stream")] public string? RequiresStream { get; set; }

    /// <summary>
    /// Applies stream-requirement attributes to the element and disables it until client-side code enables it.
    /// </summary>
    /// <remarks>
    /// If the RequiresStream property is null or empty, the output is left unchanged. Otherwise the following attributes are set on the element: <c>data-rw-requires-stream</c> (with the RequiresStream value), <c>aria-disabled="true"</c>, and <c>disabled="disabled"</c>.
    /// </remarks>
    /// <param name="context">Contextual information about the current tag processing.</param>
    /// <param name="output">The element output to modify by adding attributes.</param>
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