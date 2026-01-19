using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using ForgeTrust.Runnable.Core.Extensions;

namespace ForgeTrust.Runnable.Web.RazorWire.Bridge;

public class RazorWireStreamResult : IActionResult
{
    private readonly IEnumerable<IRazorWireStreamAction> _actions;
    private readonly Controller? _controller;

    /// <summary>
    /// Initializes a new RazorWireStreamResult that will stream the specified sequence of Razor wire actions as a Turbo Stream response.
    /// </summary>
    /// <param name="actions">Sequence of IRazorWireStreamAction instances whose rendered outputs will be written to the response in order.</param>
    /// <summary>
    /// Initializes a RazorWireStreamResult with a sequence of stream actions and an optional controller whose view context data should be reused.
    /// </summary>
    /// <param name="actions">Sequence of IRazorWireStreamAction instances to render and stream.</param>
    /// <param name="controller">Optional Controller whose ViewData and TempData will be reused during rendering; if null, fresh view/temp data will be created.</param>
    public RazorWireStreamResult(IEnumerable<IRazorWireStreamAction> actions, Controller? controller = null)
    {
        _actions = actions;
        _controller = controller;
    }

    /// <summary>
    /// Creates a RazorWireStreamResult that will stream the provided raw HTML as a single Turbo Stream action.
    /// </summary>
    /// <summary>
    /// Initializes a result that will stream the provided raw HTML as a single Razor wire action.
    /// </summary>
    /// <param name="rawContent">Raw HTML to stream; if null, an empty string is used.</param>
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
    /// <summary>
    /// Streams rendered Razor wire actions to the HTTP response as Turbo Stream HTML for the current action context.
    /// </summary>
    /// <param name="context">The action context for the current request.</param>
    /// <returns>A task that represents the execution of the result.</returns>
    /// <remarks>
    /// - Ensures antiforgery tokens (and any related cookies) are generated before writing to the response.
    /// - Sets the response Content-Type to "text/vnd.turbo-stream.html".
    /// - Builds a ViewContext and renders each configured IRazorWireStreamAction, writing each rendered HTML fragment to the response using UTF-8 encoding.
    /// - Renders actions in parallel with a maximum degree of parallelism of 64.
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

        await foreach (var html in _actions.ParallelSelectAsyncEnumerable(
                           async action => await action.RenderAsync(viewContext),
                           maxDegreeOfParallelism: 64))
        {
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
    /// <summary>
    /// Creates a ViewContext configured for rendering the stream actions, optionally inheriting ViewData and TempData from the associated controller.
    /// </summary>
    /// <param name="actionContext">The current ActionContext used to build the ViewContext.</param>
    /// <returns>A ViewContext with a NullView, the prepared ViewData, TempData (from the controller if present or created via ITempDataDictionaryFactory), TextWriter.Null, and default HtmlHelperOptions.</returns>
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

        /// <summary>
        /// Initializes a RawHtmlStreamAction that returns the provided raw HTML when rendered.
        /// </summary>
        /// <param name="html">The raw HTML content to be returned by RenderAsync.</param>
        public RawHtmlStreamAction(string html)
        {
            _html = html;
        }

        /// <summary>
/// Produces the stored raw HTML as the rendered output for the given view context.
/// </summary>
/// <param name="viewContext">The view rendering context supplied to the action.</param>
/// <returns>The rendered HTML string stored by this action.</returns>
public Task<string> RenderAsync(ViewContext viewContext) => Task.FromResult(_html);
    }

    private class NullView : IView
    {
        public string Path => string.Empty;
        /// <summary>
/// Performs no rendering and completes immediately.
/// </summary>
/// <param name="viewContext">The rendering context provided by the framework; this implementation ignores it.</param>
/// <returns>A completed Task representing the finished (no-op) render operation.</returns>
public Task RenderAsync(ViewContext viewContext) => Task.CompletedTask;
    }
}