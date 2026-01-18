using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ForgeTrust.Runnable.Web.RazorWire.TagHelpers;

[HtmlTargetElement("rw:island")]
public class IslandTagHelper : TagHelper
{
    public string Id { get; set; } = null!;

    public string? Src { get; set; }

    public string? Loading { get; set; }

    public bool Permanent { get; set; }

    public bool Swr { get; set; }

    public string? TransitionName { get; set; }

    public string? Export { get; set; }

    [HtmlAttributeName("client-module")] public string? ClientModule { get; set; }

    [HtmlAttributeName("client-strategy")] public string? ClientStrategy { get; set; }

    [HtmlAttributeName("client-props")] public string? ClientProps { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "turbo-frame";
        output.TagMode = TagMode.StartTagAndEndTag;

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

        if (!string.IsNullOrEmpty(TransitionName))
        {
            var style = output.Attributes["style"]?.Value.ToString() ?? "";
            if (!style.TrimEnd().EndsWith(";"))
            {
                style += ";";
            }

            output.Attributes.SetAttribute("style", $"{style} view-transition-name: {TransitionName};");
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
