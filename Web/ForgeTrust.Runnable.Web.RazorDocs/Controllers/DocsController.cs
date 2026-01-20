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
    /// <summary>
    /// Initializes a new instance of DocsController with the specified documentation aggregator.
    /// </summary>
    /// <param name="aggregator">The DocAggregator used to retrieve documentation items.</param>
    public DocsController(DocAggregator aggregator)
    {
        _aggregator = aggregator;
    }

    /// <summary>
    /// Displays the documentation index page.
    /// </summary>
    /// <summary>
    /// Displays the index view containing the collection of documentation items.
    /// </summary>
    /// <returns>A view result whose model is the collection of documentation items.</returns>
    public async Task<IActionResult> Index()
    {
        var docs = await _aggregator.GetDocsAsync();

        return View(docs);
    }

    /// <summary>
    /// Displays the details for a specific documentation item.
    /// </summary>
    /// <param name="path">The unique path of the document.</param>
    /// <summary>
    /// Displays the details view for a documentation item identified by the given path.
    /// </summary>
    /// <param name="path">The path or identifier of the documentation item to retrieve.</param>
    /// <returns>`IActionResult` that renders the details view with the document when found; otherwise a 404 NotFound result.</returns>
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