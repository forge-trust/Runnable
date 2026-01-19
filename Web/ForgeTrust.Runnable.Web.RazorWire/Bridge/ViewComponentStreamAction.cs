using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Encodings.Web;

namespace ForgeTrust.Runnable.Web.RazorWire.Bridge;

/// <summary>
/// Internal helper for rendering view components into Turbo Stream fragments.
/// </summary>
internal static class ViewComponentStreamHelper
{
    /// <summary>
    /// Renders a view component into a Turbo Stream XML fragment.
    /// </summary>
    /// <param name="viewContext">The current view context.</param>
    /// <param name="action">The Turbo Stream action (e.g., "replace", "append").</param>
    /// <param name="target">The DOM element identifier to update.</param>
    /// <param name="componentIdentifier">The component type or name to invoke.</param>
    /// <param name="arguments">Optional arguments to pass to the component.</param>
    /// <returns>A Turbo Stream XML string with HTML-encoded action and target attributes.</returns>
    public static async Task<string> RenderComponentStreamAsync(
        ViewContext viewContext,
        string action,
        string target,
        dynamic componentIdentifier,
        object? arguments)
    {
        await using var writer = new StringWriter();

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

        var result = await viewComponentHelper.InvokeAsync(componentIdentifier, arguments);
        result.WriteTo(writer, HtmlEncoder.Default);

        var content = writer.ToString();
        var encodedTarget = HtmlEncoder.Default.Encode(target);
        var encodedAction = HtmlEncoder.Default.Encode(action);

        return
            $"<turbo-stream action=\"{encodedAction}\" target=\"{encodedTarget}\"><template>{content}</template></turbo-stream>";
    }
}

public class ViewComponentStreamAction : IRazorWireStreamAction
{
    private readonly string _action;
    private readonly string _target;
    private readonly Type _componentType;
    private readonly object? _arguments;

    /// <summary>
    /// Creates an action that renders the specified view component type into a Turbo Stream targeting a DOM element.
    /// </summary>
    /// <param name="action">The Turbo Stream action to perform (e.g., "replace", "append", "prepend").</param>
    /// <param name="target">The DOM element id or target selector that the Turbo Stream will update.</param>
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
    /// Renders the configured view component into a Turbo Stream fragment that updates the specified target element.
    /// </summary>
    /// <param name="viewContext">The current ViewContext used as the base for rendering the view component.</param>
    /// <returns>A string containing a &lt;turbo-stream&gt; element with the <c>action</c> and <c>target</c> attributes HTML-encoded and a &lt;template&gt; containing the component's rendered HTML.</returns>
    public async Task<string> RenderAsync(ViewContext viewContext)
    {
        return await ViewComponentStreamHelper.RenderComponentStreamAsync(
            viewContext,
            _action,
            _target,
            _componentType,
            _arguments);
    }
}

public class ViewComponentByNameStreamAction : IRazorWireStreamAction
{
    private readonly string _action;
    private readonly string _target;
    private readonly string _componentName;
    private readonly object? _arguments;

    /// <summary>
    /// Initializes a ViewComponentByNameStreamAction that will render the specified view component (by name) into a Turbo Stream targeting the given DOM element.
    /// </summary>
    /// <param name="action">The Turbo Stream action to perform (e.g., "replace", "append", "prepend").</param>
    /// <param name="target">The DOM element identifier to update.</param>
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
    /// Renders the configured view component (by name) into a Turbo Stream XML fragment targeting the specified DOM element.
    /// </summary>
    /// <param name="viewContext">The current view context used as the base for creating the component's rendering context.</param>
    /// <returns>A string containing a &lt;turbo-stream&gt; element whose action and target attributes are HTML-encoded and whose &lt;template&gt; contains the rendered component HTML.</returns>
    public async Task<string> RenderAsync(ViewContext viewContext)
    {
        return await ViewComponentStreamHelper.RenderComponentStreamAsync(
            viewContext,
            _action,
            _target,
            _componentName,
            _arguments);
    }
}