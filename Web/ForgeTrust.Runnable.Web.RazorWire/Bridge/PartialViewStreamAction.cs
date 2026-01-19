using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Encodings.Web;

namespace ForgeTrust.Runnable.Web.RazorWire.Bridge;

public class PartialViewStreamAction : IRazorWireStreamAction
{
    private readonly string _action;
    private readonly string _target;
    private readonly string _viewName;
    private readonly object? _model;

    /// <summary>
    /// Initializes a new instance with the Turbo Stream action/target and the partial view to render.
    /// </summary>
    /// <param name="action">Turbo Stream action attribute (for example, "replace", "append", "update").</param>
    /// <param name="target">The target identifier for the Turbo Stream element.</param>
    /// <param name="viewName">The name or path of the partial view to render.</param>
    /// <summary>
    /// Creates an instance configured to render a specified partial view and wrap its output in a Turbo Stream element.
    /// </summary>
    /// <param name="action">The Turbo Stream action attribute (for example, "replace" or "update").</param>
    /// <param name="target">The Turbo Stream target attribute that identifies the element to update.</param>
    /// <param name="viewName">The name or path of the partial view to render.</param>
    /// <param name="model">Optional model to supply to the partial view; may be null.</param>
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

    /// <summary>
    /// Renders the specified partial view into a Turbo Stream element and returns the resulting HTML string.
    /// </summary>
    /// <param name="viewContext">The current view context used to locate services, view engines, model state, and rendering resources.</param>
    /// <returns>An HTML string representing a &lt;turbo-stream&gt; element whose &lt;template&gt; contains the rendered partial view.</returns>
    /// <summary>
    /// Renders the configured partial view into a Turbo Stream element using the provided view context.
    /// </summary>
    /// <param name="viewContext">The current view context used to locate services and render the partial view.</param>
    /// <returns>A string containing a &lt;turbo-stream&gt; element with the rendered partial view inside its &lt;template&gt;.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the partial view cannot be located.</exception>
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