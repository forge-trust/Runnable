using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web;

/// <summary>
/// Resolves and renders Runnable's conventional not-found page view.
/// </summary>
/// <remarks>
/// View resolution prefers <see cref="ConventionalNotFoundPageDefaults.AppViewPath"/> first so apps and
/// shared Razor Class Libraries can override the page conventionally. If that view is missing, the renderer
/// falls back to <see cref="ConventionalNotFoundPageDefaults.FrameworkFallbackViewPath"/>. The resolved path
/// is cached after the first successful lookup. Concurrent first-use calls may resolve the view more than
/// once, but they converge on the same immutable path and are safe.
/// </remarks>
internal sealed class ConventionalNotFoundPageRenderer
{
    private readonly IActionResultExecutor<ViewResult> _executor;
    private readonly ICompositeViewEngine _viewEngine;
    private readonly IModelMetadataProvider _metadataProvider;
    private readonly ILogger<ConventionalNotFoundPageRenderer> _logger;

    private string? _resolvedViewPath;

    /// <summary>
    /// Initializes a renderer with the MVC services required to validate and execute the conventional 404 view.
    /// </summary>
    /// <param name="executor">Executes the resolved <see cref="ViewResult"/> into the current response.</param>
    /// <param name="viewEngine">Resolves the app override view first, then the framework fallback view.</param>
    /// <param name="metadataProvider">Creates the <see cref="ViewDataDictionary"/> used for the not-found model.</param>
    /// <param name="logger">Records which conventional 404 view path was selected.</param>
    public ConventionalNotFoundPageRenderer(
        IActionResultExecutor<ViewResult> executor,
        ICompositeViewEngine viewEngine,
        IModelMetadataProvider metadataProvider,
        ILogger<ConventionalNotFoundPageRenderer> logger)
    {
        _executor = executor;
        _viewEngine = viewEngine;
        _metadataProvider = metadataProvider;
        _logger = logger;
    }

    /// <summary>
    /// Performs eager validation of the configured conventional 404 view.
    /// </summary>
    /// <remarks>
    /// Call this during startup to fail fast if neither the conventional app/shared view nor the framework
    /// fallback view can be resolved. Runtime rendering also resolves lazily, but this method turns a missing
    /// view into a predictable startup error instead of a request-time failure.
    /// </remarks>
    public void ValidateConfiguredViews()
    {
        _ = ResolveViewPath();
    }

    /// <summary>
    /// Renders the resolved conventional 404 view into the current HTTP response.
    /// </summary>
    /// <param name="httpContext">The current request context used to build the model and execute the Razor view.</param>
    /// <remarks>
    /// The rendered model is always a <see cref="NotFoundPageModel"/>. Its status defaults to 404 when the
    /// request is a direct render without a re-execute feature or reserved-route status code. This method does
    /// not change <see cref="HttpResponse.StatusCode"/> itself, which lets direct requests keep their existing
    /// 200 response and re-executed 404 requests preserve their original 404 status.
    /// </remarks>
    public async Task RenderAsync(HttpContext httpContext)
    {
        var viewData = new ViewDataDictionary(_metadataProvider, new ModelStateDictionary())
        {
            Model = CreateModel(httpContext)
        };

        var result = new ViewResult
        {
            ViewName = ResolveViewPath(),
            ViewData = viewData
        };

        var actionContext = new ActionContext(
            httpContext,
            httpContext.GetRouteData() ?? new RouteData(),
            new ActionDescriptor());

        await _executor.ExecuteAsync(actionContext, result);
    }

    private string ResolveViewPath()
    {
        if (_resolvedViewPath is not null)
        {
            return _resolvedViewPath;
        }

        var appViewResult = _viewEngine.GetView(
            executingFilePath: null,
            viewPath: ConventionalNotFoundPageDefaults.AppViewPath,
            isMainPage: true);

        if (appViewResult.Success)
        {
            _resolvedViewPath = ConventionalNotFoundPageDefaults.AppViewPath;
            _logger.LogInformation(
                "Resolved conventional 404 view to {ViewPath}.",
                _resolvedViewPath);
            return _resolvedViewPath;
        }

        var frameworkViewResult = _viewEngine.GetView(
            executingFilePath: null,
            viewPath: ConventionalNotFoundPageDefaults.FrameworkFallbackViewPath,
            isMainPage: true);

        if (frameworkViewResult.Success)
        {
            _resolvedViewPath = ConventionalNotFoundPageDefaults.FrameworkFallbackViewPath;
            _logger.LogInformation(
                "Resolved conventional 404 view to framework fallback {ViewPath}.",
                _resolvedViewPath);
            return _resolvedViewPath;
        }

        var searchedLocations = appViewResult.SearchedLocations
            .Concat(frameworkViewResult.SearchedLocations)
            .Distinct()
            .ToArray();

        var locationsMessage = searchedLocations.Length == 0
            ? "No Razor view locations were reported."
            : string.Join(Environment.NewLine, searchedLocations);

        throw new InvalidOperationException(
            $"Runnable conventional 404 pages are enabled, but neither '{ConventionalNotFoundPageDefaults.AppViewPath}' nor '{ConventionalNotFoundPageDefaults.FrameworkFallbackViewPath}' could be resolved.{Environment.NewLine}{locationsMessage}");
    }

    private static NotFoundPageModel CreateModel(HttpContext httpContext)
    {
        var reExecuteFeature = httpContext.Features.Get<IStatusCodeReExecuteFeature>();
        var statusCode = reExecuteFeature?.OriginalStatusCode
            ?? TryGetRouteStatusCode(httpContext)
            ?? StatusCodes.Status404NotFound;

        return new NotFoundPageModel(
            statusCode,
            reExecuteFeature?.OriginalPath,
            reExecuteFeature?.OriginalQueryString);
    }

    private static int? TryGetRouteStatusCode(HttpContext httpContext)
    {
        if (httpContext.GetRouteData()?.Values.TryGetValue("statusCode", out var value) != true)
        {
            return null;
        }

        return value switch
        {
            int intValue => intValue,
            string stringValue when int.TryParse(stringValue, out var parsed) => parsed,
            _ => null
        };
    }
}
