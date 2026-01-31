using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ForgeTrust.Runnable.Web.RazorWire.TagHelpers;

/// <summary>
/// Tag helper for rendering the necessary RazorWire scripts.
/// </summary>
[HtmlTargetElement("rw:scripts")]
public class RazorWireScriptsTagHelper : TagHelper
{
    private readonly IFileVersionProvider _fileVersionProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RazorWireScriptsTagHelper"/> class.
    /// </summary>
    /// <param name="fileVersionProvider">The file version provider used to append version hashes to script paths.</param>
    public RazorWireScriptsTagHelper(IFileVersionProvider fileVersionProvider)
    {
        _fileVersionProvider = fileVersionProvider;
    }

    /// <summary>
    /// Gets or sets the view context.
    /// </summary>
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    /// <summary>
    /// Renders the client-side script tags required by RazorWire and removes the wrapper element so no enclosing tag is emitted.
    /// </summary>
    /// <param name="context">The current tag helper context.</param>
    /// <param name="output">The tag helper output that will be modified to contain the script elements and have no wrapper tag.</param>
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null; // No wrapper tag

        var pathBase = ViewContext.HttpContext.Request.PathBase;

        var razorwireJs = _fileVersionProvider.AddFileVersionToPath(
            pathBase,
            "/_content/ForgeTrust.Runnable.Web.RazorWire/razorwire/razorwire.js");
        var islandsJs = _fileVersionProvider.AddFileVersionToPath(
            pathBase,
            "/_content/ForgeTrust.Runnable.Web.RazorWire/razorwire/razorwire.islands.js");

        // This includes Turbo.js and the custom RazorWire island loader.
        output.Content.SetHtmlContent(
            $@"
<script src=""https://cdn.jsdelivr.net/npm/@hotwired/turbo@8.0.12/dist/turbo.es2017-umd.js"" integrity=""sha256-1evN/OxCRDJtuVCzQ3gklVq8LzN6qhCm7x/sbawknOk="" crossorigin=""anonymous""></script>
<script src=""{razorwireJs}""></script>
<script src=""{islandsJs}""></script>
");
    }
}