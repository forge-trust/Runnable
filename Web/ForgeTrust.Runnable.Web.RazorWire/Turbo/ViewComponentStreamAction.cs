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
