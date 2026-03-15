using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Options;

namespace ForgeTrust.Runnable.Web.Tailwind.TagHelpers;

/// <summary>
/// Renders the configured Tailwind stylesheet link with cache-busting for local assets.
/// </summary>
[HtmlTargetElement("tw:styles")]
public class TailwindStylesTagHelper : TagHelper
{
    private readonly IFileVersionProvider _fileVersionProvider;
    private readonly IOptions<TailwindOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TailwindStylesTagHelper"/> class.
    /// </summary>
    /// <param name="fileVersionProvider">The file version provider used for local assets.</param>
    /// <param name="options">The configured Tailwind options.</param>
    public TailwindStylesTagHelper(
        IFileVersionProvider fileVersionProvider,
        IOptions<TailwindOptions> options)
    {
        _fileVersionProvider = fileVersionProvider ?? throw new ArgumentNullException(nameof(fileVersionProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Gets or sets the view context.
    /// </summary>
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    /// <summary>
    /// Gets or sets an optional override for the stylesheet path.
    /// </summary>
    [HtmlAttributeName("href")]
    public string? Href { get; set; }

    /// <inheritdoc />
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;

        var stylesheetPath = string.IsNullOrWhiteSpace(Href)
            ? _options.Value.StylesheetPath
            : Href;

        if (string.IsNullOrWhiteSpace(stylesheetPath))
        {
            output.SuppressOutput();
            return;
        }

        var resolvedPath = IsLocal(stylesheetPath)
            ? _fileVersionProvider.AddFileVersionToPath(ViewContext.HttpContext.Request.PathBase, stylesheetPath)
            : stylesheetPath;

        var tag = new TagBuilder("link");
        tag.Attributes["rel"] = "stylesheet";
        tag.Attributes["href"] = resolvedPath;
        tag.TagRenderMode = TagRenderMode.SelfClosing;

        using var writer = new StringWriter();
        tag.WriteTo(writer, HtmlEncoder.Default);
        output.Content.SetHtmlContent(writer.ToString());
    }

    internal static bool IsLocal(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.StartsWith("//", StringComparison.Ordinal) || path.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }

        return path[0] == '/' || path[0] == '~';
    }
}
