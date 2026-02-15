using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ForgeTrust.Runnable.Web.RazorWire;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ForgeTrust.Runnable.Web.RazorDocs.Services;

/// <summary>
/// Harvester implementation that scans C# source files for XML documentation comments.
/// </summary>
public class CSharpDocHarvester : IDocHarvester
{
    private readonly ILogger<CSharpDocHarvester> _logger;
    private static readonly string[] ExcludedDirs = { "node_modules", "bin", "obj", "Tests" };

    /// <summary>
    /// Initializes a new instance of <see cref="CSharpDocHarvester"/> with the provided logger.
    /// </summary>
    public CSharpDocHarvester(ILogger<CSharpDocHarvester> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Collects XML documentation from C# source files under the specified root and produces DocNode entries containing titles, relative file paths with anchors, and HTML-formatted content.
    /// </summary>
    /// <param name="rootPath">The root directory to recursively scan for .cs files.</param>
    /// <param name="cancellationToken">An optional token to observe for cancellation requests.</param>
    /// <returns>A collection of DocNode objects; each contains a title, a relative file path including a fragment anchor, and the extracted HTML documentation.</returns>
    public async Task<IEnumerable<DocNode>> HarvestAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        var nodes = new List<DocNode>();
        var csFiles = Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories);

        foreach (var file in csFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var segments = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Any(s => ExcludedDirs.Contains(s, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            try
            {
                var code = await File.ReadAllTextAsync(file, cancellationToken);
                var tree = CSharpSyntaxTree.ParseText(code, cancellationToken: cancellationToken);
                var root = await tree.GetRootAsync(cancellationToken);
                var relativePath = Path.GetRelativePath(rootPath, file)
                    .Replace('\\', '/'); // Normalize to forward slashes for URLs

                var fileContent = new StringBuilder();
                var hasAnyDoc = false;
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);

                // Collect stub nodes during content generation to avoid duplicate computation
                var stubNodes = new List<DocNode>();

                // Capture Classes, Structs, Interfaces, Records
                var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>().ToList();
                foreach (var typeDecl in typeDeclarations)
                {
                    var doc = ExtractDoc(typeDecl);
                    var qualifiedTypeName = GetQualifiedName(typeDecl);
                    var typeDisplayName = GetDisplayTypeName(typeDecl);
                    var typeId = StringUtils.ToSafeId(qualifiedTypeName);

                    if (doc != null)
                    {
                        hasAnyDoc = true;

                        fileContent.Append(
                            $@"<section id=""{typeId}"" class=""doc-type"">
                            <header class=""doc-type-header"">
                                <span class=""doc-kind"">Type</span>
                                <h2>{WebUtility.HtmlEncode(typeDisplayName)}</h2>
                            </header>
                            <div class=""doc-body"">
                                {doc}
                            </div>
                        </section>");

                        // Add type stub if name doesn't match filename
                        if (!string.Equals(
                                typeDecl.Identifier.Text,
                                fileNameWithoutExt,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            stubNodes.Add(
                                new DocNode(
                                    typeDisplayName,
                                    relativePath + "#" + typeId,
                                    string.Empty,
                                    relativePath));
                        }
                    }

                    var documentedMethods = typeDecl.Members
                        .OfType<MethodDeclarationSyntax>()
                        .Select(
                            method => new
                            {
                                Method = method,
                                Doc = ExtractDoc(method)
                            })
                        .Where(x => x.Doc != null)
                        .ToList();

                    if (documentedMethods.Count == 0)
                    {
                        continue;
                    }

                    hasAnyDoc = true;

                    foreach (var methodGroup in documentedMethods.GroupBy(x => x.Method.Identifier.Text))
                    {
                        var overloadCount = methodGroup.Count();
                        var overloadText = overloadCount == 1 ? "1 overload" : $"{overloadCount} overloads";

                        fileContent.Append(
                            $@"<section class=""doc-method-group"">
                            <header class=""doc-method-group-header"">
                                <span class=""doc-kind"">Method</span>
                                <h3>{WebUtility.HtmlEncode(methodGroup.Key)}</h3>
                                <span class=""doc-overload-count"">{WebUtility.HtmlEncode(overloadText)}</span>
                            </header>");

                        var index = 0;
                        foreach (var methodItem in methodGroup)
                        {
                            var method = methodItem.Method;
                            var methodDoc = methodItem.Doc!;
                            var (signature, id) = GetMethodSignatureAndId(method, qualifiedTypeName);
                            var displaySignature = GetDisplaySignature(method);
                            var openAttribute = index == 0 ? " open" : string.Empty;

                            fileContent.Append(
                                $@"<details id=""{id}"" class=""doc-overload""{openAttribute}>
                                <summary>
                                    <code>{WebUtility.HtmlEncode(displaySignature)}</code>
                                </summary>
                                <div class=""doc-overload-body"">
                                    {methodDoc}
                                </div>
                            </details>");

                            stubNodes.Add(
                                new DocNode(
                                    signature,
                                    relativePath + "#" + id,
                                    string.Empty,
                                    relativePath));

                            index++;
                        }

                        fileContent.Append("</section>");
                    }
                }

                // Capture Enums
                var enumDeclarations = root.DescendantNodes().OfType<EnumDeclarationSyntax>().ToList();
                foreach (var enumDecl in enumDeclarations)
                {
                    var doc = ExtractDoc(enumDecl);
                    if (doc != null)
                    {
                        hasAnyDoc = true;
                        var qualifiedName = GetQualifiedName(enumDecl);
                        var enumId = StringUtils.ToSafeId(qualifiedName);

                        fileContent.Append(
                            $@"<section id=""{enumId}"" class=""doc-type doc-enum"">
                            <header class=""doc-type-header"">
                                <span class=""doc-kind"">Enum</span>
                                <h2>{WebUtility.HtmlEncode(enumDecl.Identifier.Text)}</h2>
                            </header>
                            <div class=""doc-body"">
                                {doc}
                            </div>
                        </section>");

                        // Add enum stub if name doesn't match filename
                        if (!string.Equals(
                                enumDecl.Identifier.Text,
                                fileNameWithoutExt,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            stubNodes.Add(
                                new DocNode(
                                    enumDecl.Identifier.Text,
                                    relativePath + "#" + enumId,
                                    string.Empty,
                                    relativePath));
                        }
                    }
                }

                if (hasAnyDoc)
                {
                    // Add the main file node
                    nodes.Add(
                        new DocNode(
                            fileNameWithoutExt,
                            relativePath,
                            fileContent.ToString()));

                    // Add all collected stubs
                    nodes.AddRange(stubNodes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse C# file: {File}", file);
            }
        }

        return nodes;
    }

    /// <summary>
    /// Computes the method signature and safe ID for use in HTML content and stub nodes.
    /// </summary>
    /// <param name="method">The method declaration syntax.</param>
    /// <param name="qualifiedTypeName">The qualified name of the containing type.</param>
    /// <returns>A tuple containing the method signature and the safe ID.</returns>
    private static (string Signature, string Id) GetMethodSignatureAndId(
        MethodDeclarationSyntax method,
        string qualifiedTypeName)
    {
        var paramList = string.Join(
            ", ",
            method.ParameterList.Parameters.Select(p =>
                $"{p.Modifiers.ToString().Trim()} {p.Type?.ToString() ?? "object"}".Trim()));

        var typeParams = method.TypeParameterList?.ToString().Trim() ?? "";
        var explicitInterface = method.ExplicitInterfaceSpecifier?.ToString().Trim() ?? "";
        var methodName = explicitInterface + method.Identifier.Text + typeParams;
        var signature = $"{methodName}({paramList})";

        var id = StringUtils.ToSafeId($"{qualifiedTypeName}.{signature}");

        return (signature, id);
    }

    private static string GetDisplaySignature(MethodDeclarationSyntax method)
    {
        var paramList = string.Join(
            ", ",
            method.ParameterList.Parameters
                .Where(p => !IsCompilerGeneratedCallerParameter(p))
                .Select(FormatParameterForDisplay));

        var typeParams = method.TypeParameterList?.ToString().Trim() ?? string.Empty;
        var explicitInterface = method.ExplicitInterfaceSpecifier?.ToString().Trim() ?? string.Empty;
        var methodName = explicitInterface + method.Identifier.Text + typeParams;

        return $"{method.ReturnType} {methodName}({paramList})";
    }

    private static string GetDisplayTypeName(TypeDeclarationSyntax typeDecl)
    {
        var typeParams = typeDecl.TypeParameterList?.Parameters;
        if (typeParams == null || typeParams.Value.Count == 0)
        {
            return typeDecl.Identifier.Text;
        }

        var names = string.Join(", ", typeParams.Value.Select(p => p.Identifier.Text));
        return $"{typeDecl.Identifier.Text}<{names}>";
    }

    private static string GetTypeNameForQualifiedId(TypeDeclarationSyntax typeDecl)
    {
        var arity = typeDecl.TypeParameterList?.Parameters.Count ?? 0;
        return arity > 0 ? $"{typeDecl.Identifier.Text}`{arity}" : typeDecl.Identifier.Text;
    }

    private static string FormatParameterForDisplay(ParameterSyntax parameter)
    {
        var prefix = parameter.Modifiers.ToString().Trim();
        var type = parameter.Type?.ToString() ?? "object";
        var defaultValue = parameter.Default?.Value.ToString();
        var parameterDisplay = string.IsNullOrEmpty(prefix)
            ? $"{type} {parameter.Identifier.Text}"
            : $"{prefix} {type} {parameter.Identifier.Text}";

        if (!string.IsNullOrEmpty(defaultValue))
        {
            parameterDisplay += $" = {defaultValue}";
        }

        return parameterDisplay;
    }

    private static bool IsCompilerGeneratedCallerParameter(ParameterSyntax parameter)
    {
        if (parameter.Identifier.Text is "callerFilePath" or "callerLineNumber")
        {
            return true;
        }

        return parameter.AttributeLists
            .SelectMany(list => list.Attributes)
            .Select(attribute => attribute.Name.ToString())
            .Any(
                name =>
                    name.EndsWith("CallerFilePath", StringComparison.Ordinal)
                    || name.EndsWith("CallerFilePathAttribute", StringComparison.Ordinal)
                    || name.EndsWith("CallerLineNumber", StringComparison.Ordinal)
                    || name.EndsWith("CallerLineNumberAttribute", StringComparison.Ordinal));
    }

    /// <summary>
    /// Extracts XML documentation from the leading trivia of a syntax node and converts it into HTML fragments.
    /// </summary>
    /// <param name="node">The syntax node whose leading XML documentation comments will be parsed.</param>
    /// <returns>The HTML string containing structured documentation sections, or <c>null</c> if no documentation is present or parsing fails.</returns>
    private string? ExtractDoc(SyntaxNode node)
    {
        var xml = node.GetLeadingTrivia()
            .Select(i => i.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .FirstOrDefault();

        if (xml == null) return null;

        try
        {
            var cleanXml = xml.ToString().Replace("///", "").Trim();
            var wrappedXml = $"<doc>{cleanXml}</doc>";
            var xdoc = XDocument.Parse(wrappedXml, LoadOptions.PreserveWhitespace);
            var root = xdoc.Root!;

            var html = new StringBuilder();

            AppendTextSection(html, "doc-summary", root.Element("summary"));
            AppendNamedListSection(
                html,
                "doc-typeparams",
                "Type Parameters",
                root.Elements("typeparam"),
                e => e.Attribute("name")?.Value);
            AppendNamedListSection(
                html,
                "doc-params",
                "Parameters",
                root.Elements("param").Where(e => !IsCompilerGeneratedDocParameter(e.Attribute("name")?.Value)),
                e => e.Attribute("name")?.Value);
            AppendTextSection(html, "doc-returns", root.Element("returns"), "Returns");
            AppendNamedListSection(
                html,
                "doc-exceptions",
                "Exceptions",
                root.Elements("exception"),
                e => SimplifyCref(e.Attribute("cref")?.Value));
            AppendTextSection(html, "doc-remarks", root.Element("remarks"), "Remarks");
            AppendTextSection(html, "doc-example", root.Element("example"), "Example");

            var output = html.ToString();
            return string.IsNullOrWhiteSpace(output) ? null : output;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse XML documentation for node {Node}", node.ToString().Split('\n')[0]);

            return null;
        }
    }

    private static void AppendTextSection(StringBuilder html, string cssClass, XElement? section, string? heading = null)
    {
        if (section == null)
        {
            return;
        }

        var body = RenderBlockContent(section);
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        html.Append($"<div class='{cssClass}'>");
        if (!string.IsNullOrWhiteSpace(heading))
        {
            html.Append($"<h4>{WebUtility.HtmlEncode(heading)}</h4>");
        }

        html.Append(body);
        html.Append("</div>");
    }

    private static void AppendNamedListSection(
        StringBuilder html,
        string cssClass,
        string heading,
        IEnumerable<XElement> entries,
        Func<XElement, string?> keySelector)
    {
        var rows = entries
            .Select(
                entry => new
                {
                    Key = keySelector(entry)?.Trim(),
                    Description = RenderInlineContent(entry)
                })
            .Where(row => !string.IsNullOrWhiteSpace(row.Description))
            .ToList();

        if (rows.Count == 0)
        {
            return;
        }

        html.Append($"<div class='{cssClass}'>");
        html.Append($"<h4>{WebUtility.HtmlEncode(heading)}</h4>");
        html.Append("<ul>");
        foreach (var row in rows)
        {
            html.Append("<li>");
            if (!string.IsNullOrWhiteSpace(row.Key))
            {
                html.Append($"<code>{WebUtility.HtmlEncode(row.Key)}</code>");
            }

            html.Append($"<span>{row.Description}</span>");
            html.Append("</li>");
        }

        html.Append("</ul>");
        html.Append("</div>");
    }

    private static string RenderBlockContent(XElement element)
    {
        var rendered = RenderNodes(element.Nodes(), inlineContext: false).Trim();
        if (string.IsNullOrWhiteSpace(rendered))
        {
            return string.Empty;
        }

        var hasBlockChildren = element.Elements().Any(
            e => e.Name.LocalName is "para" or "code" or "list");

        return hasBlockChildren ? rendered : $"<p>{rendered}</p>";
    }

    private static string RenderInlineContent(XElement element)
    {
        return RenderNodes(element.Nodes(), inlineContext: true).Trim();
    }

    private static string RenderNodes(IEnumerable<XNode> nodes, bool inlineContext)
    {
        var builder = new StringBuilder();
        foreach (var node in nodes)
        {
            builder.Append(RenderNode(node, inlineContext));
        }

        return builder.ToString();
    }

    private static string RenderNode(XNode node, bool inlineContext)
    {
        return node switch
        {
            XText textNode => WebUtility.HtmlEncode(NormalizeWhitespace(textNode.Value)),
            XElement elementNode => RenderElement(elementNode, inlineContext),
            _ => string.Empty
        };
    }

    private static string RenderElement(XElement element, bool inlineContext)
    {
        switch (element.Name.LocalName)
        {
            case "paramref":
            case "typeparamref":
            {
                var name = element.Attribute("name")?.Value;
                return string.IsNullOrWhiteSpace(name)
                    ? string.Empty
                    : $"<code>{WebUtility.HtmlEncode(name)}</code>";
            }
            case "see":
            {
                var langword = element.Attribute("langword")?.Value;
                var cref = SimplifyCref(element.Attribute("cref")?.Value);
                var href = element.Attribute("href")?.Value;
                var displayText = langword ?? cref ?? href ?? RenderNodes(element.Nodes(), inlineContext: true).Trim();

                return string.IsNullOrWhiteSpace(displayText)
                    ? string.Empty
                    : $"<code>{WebUtility.HtmlEncode(displayText)}</code>";
            }
            case "c":
                return $"<code>{RenderNodes(element.Nodes(), inlineContext: true).Trim()}</code>";
            case "code":
                return $"<pre><code>{WebUtility.HtmlEncode(element.Value.Trim())}</code></pre>";
            case "para":
            {
                var paragraph = RenderNodes(element.Nodes(), inlineContext: true).Trim();
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    return string.Empty;
                }

                return inlineContext ? paragraph : $"<p>{paragraph}</p>";
            }
            case "list":
            {
                var listTag = string.Equals(
                    element.Attribute("type")?.Value,
                    "number",
                    StringComparison.OrdinalIgnoreCase)
                    ? "ol"
                    : "ul";

                var listItems = element.Elements("item")
                    .Select(
                        item =>
                        {
                            var description = item.Element("description");
                            var contentSource = description ?? item;
                            return RenderNodes(contentSource.Nodes(), inlineContext: true).Trim();
                        })
                    .Where(content => !string.IsNullOrWhiteSpace(content))
                    .ToList();

                if (listItems.Count == 0)
                {
                    return string.Empty;
                }

                var builder = new StringBuilder();
                builder.Append($"<{listTag}>");
                foreach (var item in listItems)
                {
                    builder.Append($"<li>{item}</li>");
                }

                builder.Append($"</{listTag}>");
                return builder.ToString();
            }
            default:
                return RenderNodes(element.Nodes(), inlineContext);
        }
    }

    private static string NormalizeWhitespace(string value)
    {
        return Regex.Replace(value, @"\s+", " ");
    }

    private static string? SimplifyCref(string? cref)
    {
        if (string.IsNullOrWhiteSpace(cref))
        {
            return null;
        }

        var simplified = cref.Trim();
        if (simplified.Length > 2 && simplified[1] == ':')
        {
            simplified = simplified[2..];
        }

        return simplified;
    }

    private static bool IsCompilerGeneratedDocParameter(string? parameterName)
    {
        return parameterName is "callerFilePath" or "callerLineNumber";
    }

    /// <summary>
    /// Builds the dot-delimited qualified name for a type or enum declaration, including enclosing types and namespaces.
    /// </summary>
    /// <param name="node">The type or enum declaration syntax node to compute the qualified name for.</param>
    /// <returns>The qualified name as a dot-delimited string containing nested type and namespace segments.</returns>
    private string GetQualifiedName(BaseTypeDeclarationSyntax node)
    {
        var parts = new Stack<string>();
        if (node is TypeDeclarationSyntax rootType)
        {
            parts.Push(GetTypeNameForQualifiedId(rootType));
        }
        else
        {
            parts.Push(node.Identifier.Text);
        }

        var parent = node.Parent;
        while (parent != null)
        {
            if (parent is TypeDeclarationSyntax typeDecl)
            {
                parts.Push(GetTypeNameForQualifiedId(typeDecl));
            }
            else if (parent is BaseNamespaceDeclarationSyntax namespaceDecl)
            {
                parts.Push(namespaceDecl.Name.ToString());
            }

            parent = parent.Parent;
        }

        return string.Join(".", parts);
    }
}
