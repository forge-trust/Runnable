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
    /// Initializes a new instance of <see cref="DocsController"/> with the specified documentation aggregator.
    /// </summary>
    /// <summary>
    /// Initializes a new DocsController with the specified documentation aggregator.
    /// </summary>
    /// <param name="aggregator">Service that provides access to documentation items used by the controller.</param>
    public DocsController(DocAggregator aggregator)
    {
        _aggregator = aggregator;
    }

    /// <summary>
    /// Displays the index view containing the collection of documentation items.
    /// </summary>
    /// <summary>
    /// Displays the documentation index view containing the available documentation items.
    /// </summary>
    /// <returns>A view result whose model is the collection of documentation items.</returns>
    public async Task<IActionResult> Index()
    {
        var docs = await _aggregator.GetDocsAsync();

        return View(docs);
    }

    /// <summary>
    /// Displays the details view for a documentation item identified by the given path.
    /// </summary>
    /// <param name="path">The unique path or identifier of the documentation item to retrieve.</param>
    /// <summary>
    /// Shows the details view for a documentation item identified by the given path.
    /// </summary>
    /// <param name="path">The documentation path used to locate the document.</param>
    /// <returns>An <see cref="IActionResult"/> that renders the details view with the located document; returns a 404 <see cref="NotFoundResult"/> if the path is null, empty, whitespace, or the document cannot be found.</returns>
    public async Task<IActionResult> Details(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return NotFound();
        }

        var doc = await _aggregator.GetDocByPathAsync(path);
        if (doc == null)
        {
            return NotFound();
        }

        return View(doc);
    }
}