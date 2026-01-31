using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace ForgeTrust.Runnable.Web.RazorWire.TagHelpers;

/// <summary>
/// A TagHelper that automatically applies version hashing to script and link tags
/// that reference local files, if the asp-append-version attribute is missing.
/// </summary>
[HtmlTargetElement("script", Attributes = "src")]
[HtmlTargetElement("link", Attributes = "href")]
public class AutoAssetVersioningTagHelper : TagHelper
{
    private const string AspAppendVersionAttributeName = "asp-append-version";
    private readonly IFileVersionProvider _fileVersionProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoAssetVersioningTagHelper"/> class.
    /// </summary>
    /// <param name="fileVersionProvider">The file version provider.</param>
    public AutoAssetVersioningTagHelper(IFileVersionProvider fileVersionProvider)
    {
        _fileVersionProvider = fileVersionProvider;
    }

    /// <summary>
    /// Gets or sets the view context.
    /// </summary>
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        // If the developer explicitly used asp-append-version (true OR false), we verify if it exists in the *HTML* attributes?
        // No, 'asp-append-version' is processed by standard TagHelpers and removed from output attributes if they run.
        // However, if standard TagHelpers ran, they would have handled it.
        // If we want to detect if it was present in the *source*, we check 'context.AllAttributes'.
        if (context.AllAttributes.ContainsName(AspAppendVersionAttributeName))
        {
            return;
        }

        if (string.Equals(output.TagName, "script", StringComparison.OrdinalIgnoreCase))
        {
            ProcessScript(output);
        }
        else if (string.Equals(output.TagName, "link", StringComparison.OrdinalIgnoreCase))
        {
            ProcessLink(output);
        }
    }

    private void ProcessScript(TagHelperOutput output)
    {
        if (output.Attributes.TryGetAttribute("src", out var srcAttribute))
        {
            var src = srcAttribute.Value?.ToString();
            if (IsLocal(src))
            {
                var newSrc = _fileVersionProvider.AddFileVersionToPath(ViewContext.HttpContext.Request.PathBase, src!);
                output.Attributes.SetAttribute("src", newSrc);
            }
        }
    }

    private void ProcessLink(TagHelperOutput output)
    {
        // Only version stylesheets
        var rel = output.Attributes["rel"]?.Value?.ToString();
        if (!string.Equals(rel, "stylesheet", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (output.Attributes.TryGetAttribute("href", out var hrefAttribute))
        {
            var href = hrefAttribute.Value?.ToString() ?? string.Empty;
            if (IsLocal(href))
            {
                output.Attributes.SetAttribute(
                    "href",
                    _fileVersionProvider.AddFileVersionToPath(ViewContext.HttpContext.Request.PathBase, href));
            }
        }
    }

    private static bool IsLocal(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return path[0] == '/' || path[0] == '~';
    }
}
