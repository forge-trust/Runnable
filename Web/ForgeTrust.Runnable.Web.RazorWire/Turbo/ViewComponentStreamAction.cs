using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Encodings.Web;

namespace ForgeTrust.Runnable.Web.RazorWire.Turbo;

public class ViewComponentStreamAction : IRazorWireStreamAction
{
    private readonly string _action;
    private readonly string _target;
    private readonly Type _componentType;
    private readonly object? _arguments;

    /// <summary>
    /// Creates a new instance configured to render the specified view component into a Turbo Stream targeting the given element.
    /// </summary>
    /// <param name="action">The Turbo Stream action to apply (for example, "replace", "append", or "prepend").</param>
    /// <param name="target">The target identifier in the DOM that the Turbo Stream will update.</param>
    /// <param name="componentType">The CLR type of the view component to invoke.</param>
    /// <param name="arguments">Optional arguments to pass to the view component when invoking it.</param>
    public ViewComponentStreamAction(
        string action,
        string target,
        Type componentType,
        object? arguments = null)
    {
        _action = action;
        _target = target;
        _componentType = componentType;
        _arguments = arguments;
    }

    /// <summary>
    /// Renders the configured view component into a Turbo Stream XML fragment that targets a DOM element.
    /// </summary>
    /// <param name="viewContext">The current view context used to execute the view component and capture its rendered HTML.</param>
    /// <returns>A string containing a &lt;turbo-stream&gt; element whose <c>action</c> and <c>target</c> attributes are HTML-encoded and whose &lt;template&gt; contains the component's rendered HTML.</returns>
    public async Task<string> RenderAsync(ViewContext viewContext)
    {
        using var writer = new StringWriter();

        // Ensure the ViewComponentHelper sees the writer we want it to write to
        var componentViewContext = new ViewContext(
            viewContext,
            viewContext.View,
            viewContext.ViewData,
            viewContext.TempData,
            writer,
            new HtmlHelperOptions()
        );

        var services = viewContext.HttpContext.RequestServices;
        var viewComponentHelper = services.GetRequiredService<IViewComponentHelper>();

        ((IViewContextAware)viewComponentHelper).Contextualize(componentViewContext);

        var result = await viewComponentHelper.InvokeAsync(_componentType, _arguments);
        result.WriteTo(writer, HtmlEncoder.Default);

        var content = writer.ToString();
        var encodedTarget = HtmlEncoder.Default.Encode(_target);
        var encodedAction = HtmlEncoder.Default.Encode(_action);

        return
            $"<turbo-stream action=\"{encodedAction}\" target=\"{encodedTarget}\"><template>{content}</template></turbo-stream>";
    }
}

public class ViewComponentByNameStreamAction : IRazorWireStreamAction
{
    private readonly string _action;
    private readonly string _target;
    private readonly string _componentName;
    private readonly object? _arguments;

    /// <summary>
    /// Initializes a new instance of <see cref="ViewComponentByNameStreamAction"/> that will render the specified view component into a Turbo Stream targeting the given target element.
    /// </summary>
    /// <param name="action">The Turbo Stream action to apply (for example, "replace", "append", or "update").</param>
    /// <param name="target">The DOM target identifier for the Turbo Stream.</param>
    /// <param name="componentName">The name of the view component to invoke.</param>
    /// <param name="arguments">Optional arguments to pass to the view component; may be null.</param>
    public ViewComponentByNameStreamAction(
        string action,
        string target,
        string componentName,
        object? arguments = null)
    {
        _action = action;
        _target = target;
        _componentName = componentName;
        _arguments = arguments;
    }

    /// <summary>
    /// Renders the configured view component (by name) into a Turbo Streams &lt;turbo-stream&gt; element containing a &lt;template&gt; with the component HTML.
    /// </summary>
    /// <param name="viewContext">The current ViewContext used to execute and render the view component.</param>
    /// <returns>A string containing a &lt;turbo-stream&gt; element whose action and target attributes are HTML-encoded and whose &lt;template&gt; contains the rendered component HTML.</returns>
    public async Task<string> RenderAsync(ViewContext viewContext)
    {
        using var writer = new StringWriter();

        var componentViewContext = new ViewContext(
            viewContext,
            viewContext.View,
            viewContext.ViewData,
            viewContext.TempData,
            writer,
            new HtmlHelperOptions()
        );

        var services = viewContext.HttpContext.RequestServices;
        var viewComponentHelper = services.GetRequiredService<IViewComponentHelper>();

        ((IViewContextAware)viewComponentHelper).Contextualize(componentViewContext);

        var result = await viewComponentHelper.InvokeAsync(_componentName, _arguments);
        result.WriteTo(writer, HtmlEncoder.Default);

        var content = writer.ToString();
        var encodedTarget = HtmlEncoder.Default.Encode(_target);
        var encodedAction = HtmlEncoder.Default.Encode(_action);

        return
            $"<turbo-stream action=\"{encodedAction}\" target=\"{encodedTarget}\"><template>{content}</template></turbo-stream>";
    }
}