using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc;

namespace ForgeTrust.Runnable.Web.RazorWire.Turbo;

public class RazorWireStreamBuilder
{
    private readonly List<IRazorWireStreamAction> _actions = new();

    public RazorWireStreamBuilder Append(string target, string templateHtml)
    {
        _actions.Add(new RawHtmlStreamAction("append", target, templateHtml));

        return this;
    }

    public RazorWireStreamBuilder AppendPartial(string target, string viewName, object? model = null)
    {
        _actions.Add(new PartialViewStreamAction("append", target, viewName, model));

        return this;
    }

    public RazorWireStreamBuilder Prepend(string target, string templateHtml)
    {
        _actions.Add(new RawHtmlStreamAction("prepend", target, templateHtml));

        return this;
    }

    public RazorWireStreamBuilder PrependPartial(string target, string viewName, object? model = null)
    {
        _actions.Add(new PartialViewStreamAction("prepend", target, viewName, model));

        return this;
    }

    public RazorWireStreamBuilder Replace(string target, string templateHtml)
    {
        _actions.Add(new RawHtmlStreamAction("replace", target, templateHtml));

        return this;
    }

    public RazorWireStreamBuilder ReplacePartial(string target, string viewName, object? model = null)
    {
        _actions.Add(new PartialViewStreamAction("replace", target, viewName, model));

        return this;
    }

    public RazorWireStreamBuilder Update(string target, string templateHtml)
    {
        _actions.Add(new RawHtmlStreamAction("update", target, templateHtml));

        return this;
    }

    public RazorWireStreamBuilder UpdatePartial(string target, string viewName, object? model = null)
    {
        _actions.Add(new PartialViewStreamAction("update", target, viewName, model));

        return this;
    }

    public RazorWireStreamBuilder AppendComponent<T>(string target, object? arguments = null) where T : ViewComponent
    {
        _actions.Add(new ViewComponentStreamAction("append", target, typeof(T), arguments));

        return this;
    }

    public RazorWireStreamBuilder PrependComponent<T>(string target, object? arguments = null) where T : ViewComponent
    {
        _actions.Add(new ViewComponentStreamAction("prepend", target, typeof(T), arguments));

        return this;
    }

    public RazorWireStreamBuilder ReplaceComponent<T>(string target, object? arguments = null) where T : ViewComponent
    {
        _actions.Add(new ViewComponentStreamAction("replace", target, typeof(T), arguments));

        return this;
    }

    public RazorWireStreamBuilder UpdateComponent<T>(string target, object? arguments = null) where T : ViewComponent
    {
        _actions.Add(new ViewComponentStreamAction("update", target, typeof(T), arguments));

        return this;
    }

    public RazorWireStreamBuilder AppendComponent(string target, string componentName, object? arguments = null)
    {
        _actions.Add(new ViewComponentByNameStreamAction("append", target, componentName, arguments));

        return this;
    }

    public RazorWireStreamBuilder PrependComponent(string target, string componentName, object? arguments = null)
    {
        _actions.Add(new ViewComponentByNameStreamAction("prepend", target, componentName, arguments));

        return this;
    }

    public RazorWireStreamBuilder ReplaceComponent(string target, string componentName, object? arguments = null)
    {
        _actions.Add(new ViewComponentByNameStreamAction("replace", target, componentName, arguments));

        return this;
    }

    public RazorWireStreamBuilder UpdateComponent(string target, string componentName, object? arguments = null)
    {
        _actions.Add(new ViewComponentByNameStreamAction("update", target, componentName, arguments));

        return this;
    }

    public RazorWireStreamBuilder Remove(string target)
    {
        _actions.Add(new RawHtmlStreamAction("remove", target, null));

        return this;
    }

    public string Build()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var action in _actions)
        {
            if (action is RawHtmlStreamAction raw)
            {
                var encodedTarget = HtmlEncoder.Default.Encode(raw.Target);
                if (raw.Action == "remove")
                    sb.Append($"<turbo-stream action=\"remove\" target=\"{encodedTarget}\"></turbo-stream>");
                else
                    sb.Append(
                        $"<turbo-stream action=\"{raw.Action}\" target=\"{encodedTarget}\"><template>{raw.Html}</template></turbo-stream>");
            }
            else
            {
                throw new InvalidOperationException(
                    "Cannot synchronously build a stream containing asynchronous actions (like Partial Views or View Components). Use RenderAsync(viewContext) or return BuildResult() from an action.");
            }
        }

        return sb.ToString();
    }

    public async Task<string> RenderAsync(Microsoft.AspNetCore.Mvc.Rendering.ViewContext viewContext)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var action in _actions)
        {
            var html = await action.RenderAsync(viewContext);
            sb.Append(html);
        }

        return sb.ToString();
    }


    public RazorWireStreamResult BuildResult()
    {
        return new RazorWireStreamResult(_actions);
    }

    private class RawHtmlStreamAction : IRazorWireStreamAction
    {
        public string Action { get; }
        public string Target { get; }
        public string? Html { get; }

        public RawHtmlStreamAction(string action, string target, string? html)
        {
            Action = action;
            Target = target;
            Html = html;
        }

        public Task<string> RenderAsync(Microsoft.AspNetCore.Mvc.Rendering.ViewContext viewContext)
        {
            var encodedTarget = HtmlEncoder.Default.Encode(Target);
            if (Action == "remove")
            {
                return Task.FromResult($"<turbo-stream action=\"remove\" target=\"{encodedTarget}\"></turbo-stream>");
            }

            return Task.FromResult(
                $"<turbo-stream action=\"{Action}\" target=\"{encodedTarget}\"><template>{Html}</template></turbo-stream>");
        }
    }
}
