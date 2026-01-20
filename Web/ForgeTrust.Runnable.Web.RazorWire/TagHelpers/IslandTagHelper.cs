using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Text.Encodings.Web;

namespace ForgeTrust.Runnable.Web.RazorWire.TagHelpers;

[HtmlTargetElement("rw:island")]
public class IslandTagHelper : TagHelper
{
    public string Id { get; set; } = string.Empty;

    public string? Src { get; set; }

    public string? Loading { get; set; }

    public bool Permanent { get; set; }

    public bool Swr { get; set; }

    public string? TransitionName { get; set; }

    public string? Export { get; set; }

    [HtmlAttributeName("client-module")] public string? ClientModule { get; set; }

    [HtmlAttributeName("client-strategy")] public string? ClientStrategy { get; set; }

    [HtmlAttributeName("client-props")] public string? ClientProps { get; set; }

    /// <summary>
    /// Renders the rw:island element as a &lt;turbo-frame&gt; and populates its attributes based on the tag helper's properties.
    /// </summary>
    /// <param name="context">The current tag helper execution context.</param>
    /// <param name="output">The output to modify; sets the tag to &lt;turbo-frame&gt; and applies id, src, loading, data-turbo-permanent, data-rw-swr, view-transition-name (appended to style), data-rw-export, and client-related data attributes according to the helper's properties.</param>
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "turbo-frame";
        output.TagMode = TagMode.StartTagAndEndTag;

        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new ArgumentException("The 'id' attribute is required for rw:island.", nameof(Id));
        }

        output.Attributes.SetAttribute("id", Id);

        if (!string.IsNullOrEmpty(Src))
        {
            output.Attributes.SetAttribute("src", Src);
        }

        if (!string.IsNullOrEmpty(Loading))
        {
            output.Attributes.SetAttribute("loading", Loading);
        }

        if (Permanent || Swr)
        {
            output.Attributes.SetAttribute("data-turbo-permanent", "");
        }

        if (Swr)
        {
            output.Attributes.SetAttribute("data-rw-swr", "true");
        }

        if (!string.IsNullOrWhiteSpace(TransitionName))
        {
            var style = output.Attributes["style"]?.Value.ToString() ?? "";
            if (!style.TrimEnd().EndsWith(";"))
            {
                style += ";";
            }

            // Sanitize transition name using centralized logic
            var safeTransitionName = StringUtils.ToSafeId(TransitionName);

            // Encoding is still a good practice even if ToSafeId ensures safe chars
            var encoded = HtmlEncoder.Default.Encode(safeTransitionName);
            output.Attributes.SetAttribute("style", $"{style} view-transition-name: {encoded};");
        }

        if (!string.IsNullOrEmpty(Export))
        {
            output.Attributes.SetAttribute("data-rw-export", Export);
        }

        if (!string.IsNullOrEmpty(ClientModule))
        {
            output.Attributes.SetAttribute("data-rw-module", ClientModule);

            if (!string.IsNullOrEmpty(ClientStrategy))
            {
                output.Attributes.SetAttribute("data-rw-strategy", ClientStrategy);
            }

            if (!string.IsNullOrEmpty(ClientProps))
            {
                output.Attributes.SetAttribute("data-rw-props", ClientProps);
            }
        }
    }
}