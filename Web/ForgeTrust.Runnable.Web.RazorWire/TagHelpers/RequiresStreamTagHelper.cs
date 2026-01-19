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
    /// <summary>
    /// Applies stream-requirement attributes to the current element when <see cref="RequiresStream"/> has a value.
    /// </summary>
    /// <param name="context">Contextual information about the current tag processing.</param>
    /// <param name="output">The element output to modify by adding attributes.</param>
    /// <remarks>
    /// If <see cref="RequiresStream"/> is null or empty, the output is left unchanged. When present, this method sets:
    /// <list type="bullet">
    /// <item><description><c>data-rw-requires-stream</c> to the value of <see cref="RequiresStream"/></description></item>
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