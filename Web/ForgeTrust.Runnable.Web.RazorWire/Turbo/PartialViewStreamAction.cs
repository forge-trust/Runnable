using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Encodings.Web;

namespace ForgeTrust.Runnable.Web.RazorWire.Turbo;

public class PartialViewStreamAction : IRazorWireStreamAction
{
    private readonly string _action;
    private readonly string _target;
    private readonly string _viewName;
    private readonly object? _model;

    public PartialViewStreamAction(
        string action,
        string target,
        string viewName,
        object? model = null)
    {
        _action = action;
        _target = target;
        _viewName = viewName;
        _model = model;
    }

    public async Task<string> RenderAsync(ViewContext viewContext)
    {
        var services = viewContext.HttpContext.RequestServices;
        var viewEngine = services.GetRequiredService<ICompositeViewEngine>();
        var tempDataProvider = services.GetRequiredService<ITempDataDictionaryFactory>();

        // We need a fresh ViewData for the partial
        var viewData =
            new ViewDataDictionary(
                new EmptyModelMetadataProvider(),
                viewContext.ModelState) { Model = _model };

        await using var writer = new StringWriter();

        var viewResult = viewEngine.FindView(viewContext, _viewName, isMainPage: false);
        if (!viewResult.Success)
        {
            viewResult = viewEngine.GetView(executingFilePath: null, _viewName, isMainPage: false);
        }

        if (!viewResult.Success)
        {
            throw new InvalidOperationException($"The partial view '{_viewName}' was not found.");
        }

        var partialViewContext = new ViewContext(
            viewContext,
            viewResult.View,
            viewData,
            tempDataProvider.GetTempData(viewContext.HttpContext),
            writer,
            new HtmlHelperOptions()
        );

        await viewResult.View.RenderAsync(partialViewContext);
        var content = writer.ToString();

        var encodedTarget = HtmlEncoder.Default.Encode(_target);
        var encodedAction = HtmlEncoder.Default.Encode(_action);

        return
            $"<turbo-stream action=\"{encodedAction}\" target=\"{encodedTarget}\"><template>{content}</template></turbo-stream>";
    }
}
