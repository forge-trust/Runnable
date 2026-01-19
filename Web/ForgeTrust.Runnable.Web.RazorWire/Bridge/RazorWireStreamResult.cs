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
    /// <summary>
    /// Initializes a new RazorWireStreamResult with the sequence of actions to render and an optional controller whose view context may be reused.
    /// </summary>
    /// <param name="actions">Actions to render and stream as Turbo Stream HTML fragments.</param>
    /// <param name="controller">Optional controller whose ViewData and TempData will be reused during rendering; if null, fresh view and temp data are created.</param>
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
    /// <summary>
    /// Creates a RazorWireStreamResult that will stream the provided raw HTML as a single render action.
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
    /// <summary>
    /// Writes rendered Turbo Stream HTML for the configured actions to the HTTP response.
    /// </summary>
    /// <param name="context">The current action context used to build the view context and access the HTTP response.</param>
    /// <remarks>
    /// Generates and stores antiforgery tokens before sending any response data, sets the response Content-Type to "text/vnd.turbo-stream.html", and streams each action's rendered HTML to the response using UTF-8 encoding. Rendering of actions is performed with limited parallelism.
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
    /// <summary>
    /// Creates a ViewContext configured for rendering the stream actions for the given action context.
    /// </summary>
    /// <param name="actionContext">The current ActionContext used to populate the returned ViewContext.</param>
    /// <returns>A ViewContext configured with a NullView, the prepared ViewData, TempData (the controller's if available or obtained from ITempDataDictionaryFactory), TextWriter.Null, and default HtmlHelperOptions.</returns>
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
        /// <summary>
        /// Initializes a new RawHtmlStreamAction that will return the specified HTML when rendered.
        /// </summary>
        /// <param name="html">HTML content that RenderAsync will return (can be empty).</param>
        public RawHtmlStreamAction(string html)
        {
            _html = html;
        }

        /// <summary>
/// Produces the stored raw HTML as the rendered output for the given view context.
/// </summary>
/// <param name="viewContext">The view rendering context supplied to the action.</param>
/// <summary>
/// Provide the stored HTML string as the rendered result.
/// </summary>
/// <param name="viewContext">The view context supplied for rendering; this implementation does not use it.</param>
/// <returns>The stored HTML string.</returns>
public Task<string> RenderAsync(ViewContext viewContext) => Task.FromResult(_html);
    }

    private class NullView : IView
    {
        public string Path => string.Empty;
        /// <summary>
/// Performs no rendering and completes immediately.
/// </summary>
/// <param name="viewContext">The rendering context provided by the framework; this implementation ignores it.</param>
/// <summary>
/// A no-op renderer that performs no output when asked to render a view.
/// </summary>
/// <param name="viewContext">The view context provided for rendering; this implementation ignores it.</param>
/// <returns>A completed <see cref="Task"/> representing the finished render operation.</returns>
public Task RenderAsync(ViewContext viewContext) => Task.CompletedTask;
    }
}