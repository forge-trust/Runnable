using System.Text.Encodings.Web;

namespace ForgeTrust.Runnable.Web.RazorWire.Turbo;

public class TurboStreamBuilder
{
    private readonly List<string> _streams = new();

    public TurboStreamBuilder Append(string target, string templateHtml)
    {
        _streams.Add($"<turbo-stream action=\"append\" target=\"{HtmlEncoder.Default.Encode(target)}\"><template>{templateHtml}</template></turbo-stream>");
        return this;
    }

    public TurboStreamBuilder Prepend(string target, string templateHtml)
    {
        _streams.Add($"<turbo-stream action=\"prepend\" target=\"{HtmlEncoder.Default.Encode(target)}\"><template>{templateHtml}</template></turbo-stream>");
        return this;
    }

    public TurboStreamBuilder Replace(string target, string templateHtml)
    {
        _streams.Add($"<turbo-stream action=\"replace\" target=\"{HtmlEncoder.Default.Encode(target)}\"><template>{templateHtml}</template></turbo-stream>");
        return this;
    }

    public TurboStreamBuilder Update(string target, string templateHtml)
    {
        _streams.Add($"<turbo-stream action=\"update\" target=\"{HtmlEncoder.Default.Encode(target)}\"><template>{templateHtml}</template></turbo-stream>");
        return this;
    }

    public TurboStreamBuilder Remove(string target)
    {
        _streams.Add($"<turbo-stream action=\"remove\" target=\"{HtmlEncoder.Default.Encode(target)}\"></turbo-stream>");
        return this;
    }

    public TurboStreamBuilder Before(string target, string templateHtml)
    {
        _streams.Add($"<turbo-stream action=\"before\" target=\"{HtmlEncoder.Default.Encode(target)}\"><template>{templateHtml}</template></turbo-stream>");
        return this;
    }

    public TurboStreamBuilder After(string target, string templateHtml)
    {
        _streams.Add($"<turbo-stream action=\"after\" target=\"{HtmlEncoder.Default.Encode(target)}\"><template>{templateHtml}</template></turbo-stream>");
        return this;
    }

    public string Build()
    {
        return string.Join("\n", _streams);
    }
}
