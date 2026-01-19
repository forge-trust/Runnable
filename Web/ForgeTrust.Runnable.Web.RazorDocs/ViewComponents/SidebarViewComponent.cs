using Microsoft.AspNetCore.Mvc;
using ForgeTrust.Runnable.Web.RazorDocs.Services;

namespace ForgeTrust.Runnable.Web.RazorDocs.ViewComponents;

public class SidebarViewComponent : ViewComponent
{
    private readonly DocAggregator _aggregator;

    public SidebarViewComponent(DocAggregator aggregator)
    {
        _aggregator = aggregator;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var docs = await _aggregator.GetDocsAsync();
        var groupedDocs = docs.GroupBy(d => Path.GetDirectoryName(d.Path) ?? "General")
            .OrderBy(g => g.Key);

        return View(groupedDocs);
    }
}
