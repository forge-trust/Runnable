using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Web.RazorWire.Bridge;

public class RazorWireStreamResult : IActionResult
{
    private readonly IEnumerable<IRazorWireStreamAction> _actions;
    private readonly Controller? _controller;

    public RazorWireStreamResult(IEnumerable<IRazorWireStreamAction> actions, Controller? controller = null)
    {
        _actions = actions;
        _controller = controller;
    }

    public RazorWireStreamResult(string? rawContent)
    {
        _actions = [new RawHtmlStreamAction(rawContent ?? string.Empty)];
    }

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var services = context.HttpContext.RequestServices;

        // CRITICAL: Generate antiforgery tokens BEFORE we start streaming
        // This ensures any required cookies are set before headers are sent
        var antiforgery = services.GetService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>();
        if (antiforgery != null)
        {
            // This will generate and set the antiforgery cookie if needed
            _ = antiforgery.GetAndStoreTokens(context.HttpContext);
        }

        var response = context.HttpContext.Response;
        response.ContentType = "text/vnd.turbo-stream.html";

        var viewContext = CreateViewContext(context);

        foreach (var action in _actions)
        {
            var html = await action.RenderAsync(viewContext);
            await response.WriteAsync(html, Encoding.UTF8);
        }
    }

    private ViewContext CreateViewContext(ActionContext actionContext)
    {
        var services = actionContext.HttpContext.RequestServices;

        ViewDataDictionary viewData;
        ITempDataDictionary? tempData = null;

        var tempDataProvider = services.GetRequiredService<ITempDataDictionaryFactory>();

        // If we have a controller instance, inherit its ViewData and TempData
        if (_controller != null)
        {
            viewData = new ViewDataDictionary(_controller.ViewData);
            tempData = _controller.TempData;
        }
        else
        {
            viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), actionContext.ModelState);
        }

        return new ViewContext(
            actionContext,
            new NullView(),
            viewData,
            tempData ?? tempDataProvider.GetTempData(actionContext.HttpContext),
            TextWriter.Null,
            new HtmlHelperOptions()
        );
    }

    private class RawHtmlStreamAction : IRazorWireStreamAction
    {
        private readonly string _html;

        public RawHtmlStreamAction(string html)
        {
            _html = html;
        }

        public Task<string> RenderAsync(ViewContext viewContext) => Task.FromResult(_html);
    }

    private class NullView : IView
    {
        public string Path => string.Empty;
        public Task RenderAsync(ViewContext viewContext) => Task.CompletedTask;
    }
}
