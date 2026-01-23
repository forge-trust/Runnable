using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ForgeTrust.Runnable.Web.RazorWire.TagHelpers;

/// <summary>
/// A Tag Helper that enhances a standard <c>&lt;form&gt;</c> element with RazorWire/Turbo features.
/// </summary>
[HtmlTargetElement("form", Attributes = "rw-active")]
public class RazorWireFormTagHelper : TagHelper
{
    /// <summary>
    /// Gets or sets a value indicating whether RazorWire/Turbo enhancement is enabled for this form.
    /// Defaults to <c>true</c>.
    /// </summary>
    [HtmlAttributeName("rw-active")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the identifier of the Turbo Frame that this form should target.
    /// </summary>
    [HtmlAttributeName("rw-target")]
    public string? TargetFrame { get; set; }


    /// <summary>
    /// Processes a form tag by removing attributes that start with "rw-" and configuring Turbo attributes based on the tag helper's properties.
    /// </summary>
    /// <param name="context">The context for the current tag helper execution.</param>
    /// <param name="output">The tag helper output whose attributes will be modified.</param>
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var attributesToRemove = output.Attributes
            .Where(a => a.Name.StartsWith("rw-", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var attr in attributesToRemove)
        {
            output.Attributes.Remove(attr);
        }

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