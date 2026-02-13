using Microsoft.AspNetCore.Mvc;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using System.Net;
using System.Text.RegularExpressions;

namespace ForgeTrust.Runnable.Web.RazorDocs.Controllers;

/// <summary>
/// Controller for serving documentation pages.
/// </summary>
public class DocsController : Controller
{
    private static readonly Regex ScriptOrStyleRegex = new(
        "<(script|style)[^>]*>.*?</\\1>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex TagRegex = new(
        "<[^>]+>",
        RegexOptions.Compiled);

    private static readonly Regex H2H3Regex = new(
        "<h[23][^>]*>(.*?)</h[23]>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex MultiSpaceRegex = new(
        "\\s+",
        RegexOptions.Compiled);

    private readonly DocAggregator _aggregator;

    /// <summary>
    /// Initializes a new instance of <see cref="DocsController"/> with the specified documentation aggregator.
    /// </summary>
    /// <param name="aggregator">Service used to retrieve documentation items.</param>
    public DocsController(DocAggregator aggregator)
    {
        _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
    }

    /// <summary>
    /// Displays the documentation index view containing available documentation items.
    /// </summary>
    /// <returns>A view result whose model is the collection of documentation items extracted from the repository.</returns>
    public async Task<IActionResult> Index()
    {
        var docs = await _aggregator.GetDocsAsync(HttpContext.RequestAborted);

        return View(docs);
    }

    /// <summary>
    /// Displays the details view for a documentation item identified by the given path.
    /// </summary>
    /// <param name="path">The unique path or identifier of the documentation item to retrieve.</param>
    /// <returns>An <see cref="IActionResult"/> rendering the details view with the document; returns <see cref="NotFoundResult"/> if the path is invalid or the document is missing.</returns>
    public async Task<IActionResult> Details(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return NotFound();
        }

        var doc = await _aggregator.GetDocByPathAsync(path, HttpContext.RequestAborted);
        if (doc == null)
        {
            return NotFound();
        }

        return View(doc);
    }

    /// <summary>
    /// Displays the dedicated docs search page.
    /// </summary>
    public IActionResult Search()
    {
        return View();
    }

    /// <summary>
    /// Returns docs search index data for live-hosted docs.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SearchIndex()
    {
        var docs = await _aggregator.GetDocsAsync(HttpContext.RequestAborted);
        var records = docs
            .Select(d =>
            {
                var content = d.Content ?? string.Empty;
                var bodyText = NormalizeText(TagRegex.Replace(ScriptOrStyleRegex.Replace(content, string.Empty), " "));
                var snippet = bodyText.Length > 220 ? bodyText[..220].TrimEnd() + "..." : bodyText;

                var headings = H2H3Regex.Matches(content)
                    .Select(m => NormalizeText(TagRegex.Replace(m.Groups[1].Value, " ")))
                    .Where(h => !string.IsNullOrWhiteSpace(h))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(24)
                    .ToList();

                return new
                {
                    id = d.Path,
                    path = $"/docs/{d.Path}",
                    title = d.Title,
                    headings,
                    bodyText,
                    snippet
                };
            })
            .Where(r => !string.IsNullOrWhiteSpace(r.title) || !string.IsNullOrWhiteSpace(r.bodyText))
            .GroupBy(r => r.path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(r => r.path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Json(new
        {
            metadata = new
            {
                generatedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                version = "1",
                engine = "minisearch"
            },
            documents = records
        });
    }

    private static string NormalizeText(string text)
    {
        var decoded = WebUtility.HtmlDecode(text ?? string.Empty);
        return MultiSpaceRegex.Replace(decoded, " ").Trim();
    }
}
