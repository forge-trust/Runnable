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

    /// <summary>
    /// Initializes a new RazorWireStreamResult that will stream the specified sequence of Razor wire actions as a Turbo Stream response.
    /// </summary>
    /// <param name="actions">Sequence of IRazorWireStreamAction instances whose rendered outputs will be written to the response in order.</param>
    public RazorWireStreamResult(IEnumerable<IRazorWireStreamAction> actions)
    {
        _actions = actions;
    }

    /// <summary>
    /// Creates a RazorWireStreamResult that will stream the provided raw HTML as a single Turbo Stream action.
    /// </summary>
    /// <param name="rawContent">The raw HTML to stream; if null, an empty string is used.</param>
    public RazorWireStreamResult(string? rawContent)
    {
        _actions = [new RawHtmlStreamAction(rawContent ?? string.Empty)];
    }

    /// <summary>
    /// Streams the rendered HTML from each configured IRazorWireStreamAction to the HTTP response as a Turbo Stream.
    /// </summary>
    /// <param name="context">The current ActionContext used to build the view context and write the response.</param>
    /// <returns>A task that completes when all actions have been rendered and written to the response.</returns>
    /// <remarks>
    /// Sets the response Content-Type to "text/vnd.turbo-stream.html" and, if an antiforgery service is available,
    /// generates and stores antiforgery tokens before writing any response data.
    /// </remarks>
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

    /// <summary>
    /// Create a ViewContext for rendering Razor stream actions based on the given ActionContext.
    /// </summary>
    /// <param name="actionContext">The action context whose HttpContext and ModelState are used to initialize the view context.</param>
    /// <returns>
    /// A ViewContext configured with:
    /// - a NullView for the IView,
    /// - a ViewDataDictionary created from an EmptyModelMetadataProvider and the provided ModelState,
    /// - TempData obtained from the request's ITempDataDictionaryFactory,
    /// - TextWriter.Null as the writer,
    /// - default HtmlHelperOptions.
    /// </returns>
    private ViewContext CreateViewContext(ActionContext actionContext)
    {
        var services = actionContext.HttpContext.RequestServices;
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), actionContext.ModelState);

        var tempDataProvider = services.GetRequiredService<ITempDataDictionaryFactory>();

        // If the context is from a controller, try to inherit its ViewData
        if (actionContext is ControllerContext controllerContext
            && controllerContext.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor
                actionDescriptor)
        {
            // TODO: Implement this
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