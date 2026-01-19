using Microsoft.AspNetCore.Mvc;
using ForgeTrust.Runnable.Web.RazorDocs.Services;

namespace ForgeTrust.Runnable.Web.RazorDocs.Controllers;

/// <summary>
/// Controller for serving documentation pages.
/// </summary>
public class DocsController : Controller
{
    private readonly DocAggregator _aggregator;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocsController"/> class.
    /// </summary>
    /// <param name="aggregator">The documentation aggregator service.</param>
    public DocsController(DocAggregator aggregator)
    {
        _aggregator = aggregator;
    }

    /// <summary>
    /// Displays the documentation index page.
    /// </summary>
    /// <returns>The index view.</returns>
    public async Task<IActionResult> Index()
    {
        var docs = await _aggregator.GetDocsAsync();

        return View(docs);
    }

    /// <summary>
    /// Displays the details for a specific documentation item.
    /// </summary>
    /// <param name="path">The unique path of the document.</param>
    /// <returns>The details view or 404 if not found.</returns>
    public async Task<IActionResult> Details(string path)
    {
        var doc = await _aggregator.GetDocByPathAsync(path);
        if (doc == null)
        {
            return NotFound();
        }

        return View(doc);
    }
}
