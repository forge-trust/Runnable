using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc;

namespace ForgeTrust.Runnable.Web.RazorWire.Bridge;

public class RazorWireStreamBuilder
{
    private readonly Controller? _controller;
    private readonly List<IRazorWireStreamAction> _actions = new();

    public RazorWireStreamBuilder(Controller? controller = null)
    {
        _controller = controller;
    }

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

    /// <summary>
    /// Builds a concatenated string of Turbo Stream elements for the queued raw HTML actions.
    /// </summary>
    /// <returns>A string containing the concatenated &lt;turbo-stream&gt; elements for all raw HTML actions.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the builder contains actions that require asynchronous rendering (such as partial views or view components); use RenderAsync(viewContext) or BuildResult() instead.</exception>
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
                    """
                    Cannot synchronously build a stream containing asynchronous actions
                    (like Partial Views or View Components).
                    Use RenderAsync(viewContext) or return BuildResult() from an action.
                    """);
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


    /// <summary>
    /// Creates a RazorWireStreamResult that contains the builder's queued stream actions.
    /// </summary>
    /// <returns>A RazorWireStreamResult whose actions list is a snapshot copy of the builder's queued actions.</returns>
    public RazorWireStreamResult BuildResult()
    {
        return new RazorWireStreamResult(_actions.ToList(), _controller);
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

        /// <summary>
        /// Renders the turbo-stream element for this raw HTML action.
        /// </summary>
        /// <returns>The turbo-stream element for the action and target: for action &quot;remove&quot; a remove stream without a template; otherwise a stream whose &lt;template&gt; contains the action's HTML.</returns>
        public Task<string> RenderAsync(Microsoft.AspNetCore.Mvc.Rendering.ViewContext viewContext)
        {
            var encodedTarget = HtmlEncoder.Default.Encode(Target);
            if (Action == "remove")
            {
                return Task.FromResult(
                    $"<turbo-stream action=\"remove\" target=\"{encodedTarget}\">"
                    + $"</turbo-stream>");
            }

            return Task.FromResult(
                $"<turbo-stream action=\"{Action}\" target=\"{encodedTarget}\">"
                + $"<template>{Html}</template></turbo-stream>");
        }
    }
}