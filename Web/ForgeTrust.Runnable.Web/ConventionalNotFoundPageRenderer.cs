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
/// Resolves and renders Runnable's conventional not-found page.
/// </summary>
internal sealed class ConventionalNotFoundPageRenderer
{
    private readonly IActionResultExecutor<ViewResult> _executor;
    private readonly ICompositeViewEngine _viewEngine;
    private readonly IModelMetadataProvider _metadataProvider;
    private readonly ILogger<ConventionalNotFoundPageRenderer> _logger;

    private string? _resolvedViewPath;

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

    public void ValidateConfiguredViews()
    {
        _ = ResolveViewPath();
    }

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
