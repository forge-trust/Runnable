using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;

namespace ForgeTrust.Runnable.Web.RazorWire.Bridge;

/// <summary>
/// Implements <see cref="IRazorPartialRenderer"/> using the Razor view engine.
/// </summary>
internal class RazorPartialRenderer : IRazorPartialRenderer
{
    private readonly IRazorViewEngine _viewEngine;
    private readonly ITempDataDictionaryFactory _tempDataFactory;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RazorPartialRenderer"/> class.
    /// </summary>
    /// <param name="viewEngine">The Razor view engine used to locate partials.</param>
    /// <param name="tempDataFactory">The factory used to create temporary data dictionaries for rendering.</param>
    /// <param name="serviceProvider">The service provider used to resolve dependencies for the rendering context.</param>
    public RazorPartialRenderer(
        IRazorViewEngine viewEngine,
        ITempDataDictionaryFactory tempDataFactory,
        IServiceProvider serviceProvider)
    {
        _viewEngine = viewEngine;
        _tempDataFactory = tempDataFactory;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Because this method executes outside of an HTTP request context, it uses a blank <see cref="ActionContext"/>
    /// (with empty <see cref="RouteData"/> and <see cref="ActionDescriptor"/>). As a result, the
    /// <see cref="IRazorViewEngine"/> may not be able to locate views that are scoped to a specific controller
    /// (e.g., <c>Views/ControllerName/ViewName.cshtml</c>) unless the full path is provided (e.g., <c>~/Views/Reactivity/_MyPartial.cshtml</c>).
    /// Shared views (e.g., <c>Views/Shared/_MyPartial.cshtml</c>) are generally resolvable by name.
    /// </para>
    /// </remarks>
    public async Task<string> RenderPartialToStringAsync(
        string viewName,
        object? model = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var httpContext = new DefaultHttpContext { RequestServices = _serviceProvider };
        httpContext.RequestAborted = cancellationToken;

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        await using var sw = new StringWriter();
        var viewResult = _viewEngine.FindView(actionContext, viewName, isMainPage: false);

        if (!viewResult.Success)
        {
            throw new InvalidOperationException($"Could not find view with name '{viewName}'.");
        }

        var viewDictionary =
            new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model };

        var viewContext = new ViewContext(
            actionContext,
            viewResult.View,
            viewDictionary,
            _tempDataFactory.GetTempData(httpContext),
            sw,
            new HtmlHelperOptions()
        );

        await viewResult.View.RenderAsync(viewContext);

        return sw.ToString();
    }
}
