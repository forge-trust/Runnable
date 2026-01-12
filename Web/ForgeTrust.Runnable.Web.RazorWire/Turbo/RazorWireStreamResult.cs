using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeTrust.Runnable.Web.RazorWire.Turbo;

public class RazorWireStreamResult : IActionResult
{
    private readonly IEnumerable<IRazorWireStreamAction> _actions;

    public RazorWireStreamResult(IEnumerable<IRazorWireStreamAction> actions)
    {
        _actions = actions;
    }

    public RazorWireStreamResult(string rawContent)
    {
        _actions = new[] { new RawHtmlStreamAction(rawContent) };
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
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), actionContext.ModelState);
        
        var tempDataProvider = services.GetRequiredService<ITempDataDictionaryFactory>();
        
        // If the context is from a controller, try to inherit its ViewData
        if (actionContext is ControllerContext controllerContext && controllerContext.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor)
        {
            // Note: This is a bit simplified, in a real bridge we'd pass the actual controller's ViewData
        }

        return new ViewContext(
            actionContext,
            new NullView(),
            viewData,
            tempDataProvider.GetTempData(actionContext.HttpContext),
            TextWriter.Null,
            new HtmlHelperOptions()
        );
    }

    private class RawHtmlStreamAction : IRazorWireStreamAction
    {
        private readonly string _html;
        public RawHtmlStreamAction(string html) => _html = html;
        public Task<string> RenderAsync(ViewContext viewContext) => Task.FromResult(_html);
    }

    private class NullView : IView
    {
        public string Path => string.Empty;
        public Task RenderAsync(ViewContext viewContext) => Task.CompletedTask;
    }
}
