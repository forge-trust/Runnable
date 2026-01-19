using Microsoft.AspNetCore.Mvc;
using ForgeTrust.Runnable.Web.RazorDocs.Services;

namespace ForgeTrust.Runnable.Web.RazorDocs.Controllers;

public class DocsController : Controller
{
    private readonly DocAggregator _aggregator;

    public DocsController(DocAggregator aggregator)
    {
        _aggregator = aggregator;
    }

    public async Task<IActionResult> Index()
    {
        var docs = await _aggregator.GetDocsAsync();

        return View(docs);
    }

    public async Task<IActionResult> Details(string path)
    {
        var docs = await _aggregator.GetDocsAsync();
        var doc = docs.FirstOrDefault(d => d.Path == path);
        if (doc == null)
        {
            return NotFound();
        }

        return View(doc);
    }
}
