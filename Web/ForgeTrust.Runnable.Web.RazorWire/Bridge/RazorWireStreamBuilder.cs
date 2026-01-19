using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc;

namespace ForgeTrust.Runnable.Web.RazorWire.Bridge;

public class RazorWireStreamBuilder
{
    private readonly Controller? _controller;
    private readonly List<IRazorWireStreamAction> _actions = new();

    /// <summary>
    /// Initializes a new instance of <see cref="RazorWireStreamBuilder"/> and optionally associates a controller for later rendering.
    /// </summary>
    /// <param name="controller">An optional <see cref="Controller"/> whose context will be captured for rendering partials or view components; may be <c>null</c>.</param>
    public RazorWireStreamBuilder(Controller? controller = null)
    {
        _controller = controller;
    }

    /// <summary>
    /// Queues an append stream action that inserts the provided HTML into the specified target element.
    /// </summary>
    /// <param name="target">The target DOM selector or element identifier to which the HTML will be appended.</param>
    /// <param name="templateHtml">The HTML fragment to append into the target element.</param>
    /// <returns>The same <see cref="RazorWireStreamBuilder"/> instance to allow fluent chaining.</returns>
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

    /// <summary>
    /// Queue a remove action that will remove the element identified by the specified target.
    /// </summary>
    /// <param name="target">The DOM target selector or element identifier to remove.</param>
    /// <returns>The current <see cref="RazorWireStreamBuilder"/> instance for fluent chaining.</returns>
    public RazorWireStreamBuilder Remove(string target)
    {
        _actions.Add(new RawHtmlStreamAction("remove", target, null));

        return this;
    }

    /// <summary>
    /// Builds a single string containing the queued turbo-stream elements for all raw HTML actions.
    /// </summary>
    /// <returns>The concatenated turbo-stream markup for the builder's raw HTML actions.</returns>
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

    /// <summary>
    /// Renders all queued stream actions using the given view context and returns their combined output.
    /// </summary>
    /// <param name="viewContext">The view rendering context used by each action to produce its HTML.</param>
    /// <returns>The concatenated HTML string produced by rendering each queued action.</returns>
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
    /// Creates a RazorWireStreamResult containing a snapshot of the builder's queued actions and the optional controller.
    /// </summary>
    /// <returns>A RazorWireStreamResult initialized with a copy of the queued actions and the builder's controller.</returns>
    public RazorWireStreamResult BuildResult()
    {
        return new RazorWireStreamResult(_actions.ToList(), _controller);
    }

    private class RawHtmlStreamAction : IRazorWireStreamAction
    {
        public string Action { get; }
        public string Target { get; }
        public string? Html { get; }

        /// <summary>
        /// Initializes a new instance with the specified stream action, target, and optional HTML template.
        /// </summary>
        /// <param name="action">The turbo-stream action name (e.g., "append", "prepend", "replace", "update", "remove").</param>
        /// <param name="target">The DOM target identifier or selector to which the action applies.</param>
        /// <param name="html">The HTML template to use for the action; pass <c>null</c> for actions that do not require a template (such as "remove").</param>
        public RawHtmlStreamAction(string action, string target, string? html)
        {
            Action = action;
            Target = target;
            Html = html;
        }

        /// <summary>
        /// Produces the turbo-stream element representing this action and its target.
        /// </summary>
        /// <returns>The turbo-stream element for the action and target; for action &quot;remove&quot; a stream without a &lt;template&gt;, otherwise a stream whose &lt;template&gt; contains the action's HTML.</returns>
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