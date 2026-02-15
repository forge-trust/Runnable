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
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

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
        var stubNodes = new List<DocNode>();
        var namespacePages = new Dictionary<string, NamespaceDocPage>(StringComparer.OrdinalIgnoreCase);
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

                // Capture Classes, Structs, Interfaces, Records
                var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>().ToList();
                foreach (var typeDecl in typeDeclarations)
                {
                    var doc = ExtractDoc(typeDecl);
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
                    var documentedProperties = typeDecl.Members
                        .OfType<PropertyDeclarationSyntax>()
                        .Select(
                            property => new
                            {
                                Property = property,
                                Doc = ExtractDoc(property)
                            })
                        .Where(x => x.Doc != null)
                        .ToList();

                    if (doc == null && documentedMethods.Count == 0 && documentedProperties.Count == 0)
                    {
                        continue;
                    }

                    var qualifiedTypeName = GetQualifiedName(typeDecl);
                    var typeDisplayName = GetDisplayTypeName(typeDecl);
                    var typeId = StringUtils.ToSafeId(qualifiedTypeName);
                    var namespacePage = GetOrCreateNamespacePage(namespacePages, GetNamespaceName(typeDecl));

                    namespacePage.Content.Append(
                        $@"<section id=""{typeId}"" class=""doc-type"">
                        <header class=""doc-type-header"">
                            <span class=""doc-kind"">Type</span>
                            <h2>{WebUtility.HtmlEncode(typeDisplayName)}</h2>
                        </header>");

                    if (!string.IsNullOrWhiteSpace(doc))
                    {
                        namespacePage.Content.Append(
                            $@"<div class=""doc-body"">
                            {doc}
                        </div>");
                    }

                    stubNodes.Add(
                        new DocNode(
                            typeDisplayName,
                            namespacePage.Path + "#" + typeId,
                            string.Empty,
                            namespacePage.Path));

                    foreach (var methodGroup in documentedMethods.GroupBy(x => x.Method.Identifier.Text))
                    {
                        var overloadCount = methodGroup.Count();

                        namespacePage.Content.Append(
                            $@"<section class=""doc-method-group"">
                            <header class=""doc-method-group-header"">
                                <span class=""doc-kind"">Method</span>
                                <h3>{WebUtility.HtmlEncode(methodGroup.Key)}</h3>");

                        if (overloadCount > 1)
                        {
                            namespacePage.Content.Append(
                                $@"<span class=""doc-overload-count"">{WebUtility.HtmlEncode($"{overloadCount} overloads")}</span>");
                        }

                        namespacePage.Content.Append("</header>");

                        var index = 0;
                        foreach (var methodItem in methodGroup)
                        {
                            var method = methodItem.Method;
                            var methodDoc = methodItem.Doc!;
                            var (_, id) = GetMethodSignatureAndId(method, qualifiedTypeName);
                            var highlightedDisplaySignature = GetHighlightedDisplaySignature(method);
                            var openAttribute = index == 0 ? " open" : string.Empty;

                            namespacePage.Content.Append(
                                $@"<details id=""{id}"" class=""doc-overload""{openAttribute}>
                                <summary>
                                    <code class=""doc-signature"">{highlightedDisplaySignature}</code>
                                </summary>
                                <div class=""doc-overload-body"">
                                    {methodDoc}
                                </div>
                            </details>");

                            index++;
                        }

                        namespacePage.Content.Append("</section>");
                    }

                    foreach (var propertyItem in documentedProperties)
                    {
                        var property = propertyItem.Property;
                        var propertyDoc = propertyItem.Doc!;
                        var (_, id) = GetPropertySignatureAndId(property, qualifiedTypeName);
                        var highlightedPropertySignature = GetHighlightedPropertySignature(property);

                        namespacePage.Content.Append(
                            $@"<section class=""doc-method-group"">
                            <header class=""doc-method-group-header"">
                                <span class=""doc-kind"">Property</span>
                                <h3>{WebUtility.HtmlEncode(property.Identifier.Text)}</h3>
                            </header>
                            <article id=""{id}"" class=""doc-overload doc-property"">
                                <div class=""doc-property-signature"">
                                    <code class=""doc-signature"">{highlightedPropertySignature}</code>
                                </div>
                                <div class=""doc-overload-body"">
                                    {propertyDoc}
                                </div>
                            </article>
                        </section>");
                    }

                    namespacePage.Content.Append("</section>");
                }

                // Capture Enums
                var enumDeclarations = root.DescendantNodes().OfType<EnumDeclarationSyntax>().ToList();
                foreach (var enumDecl in enumDeclarations)
                {
                    var doc = ExtractDoc(enumDecl);
                    if (doc != null)
                    {
                        var namespacePage = GetOrCreateNamespacePage(namespacePages, GetNamespaceName(enumDecl));
                        var qualifiedName = GetQualifiedName(enumDecl);
                        var enumId = StringUtils.ToSafeId(qualifiedName);

                        namespacePage.Content.Append(
                            $@"<section id=""{enumId}"" class=""doc-type doc-enum"">
                            <header class=""doc-type-header"">
                                <span class=""doc-kind"">Enum</span>
                                <h2>{WebUtility.HtmlEncode(enumDecl.Identifier.Text)}</h2>
                            </header>
                            <div class=""doc-body"">
                                {doc}
                            </div>
                        </section>");

                        stubNodes.Add(
                            new DocNode(
                                enumDecl.Identifier.Text,
                                namespacePage.Path + "#" + enumId,
                                string.Empty,
                                namespacePage.Path));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse C# file: {File}", file);
            }
        }

        EnsureNamespaceHierarchy(namespacePages);

        foreach (var namespacePage in namespacePages.Values.OrderBy(p => p.Path, StringComparer.OrdinalIgnoreCase))
        {
            if (namespacePage.Content.Length == 0)
            {
                continue;
            }

            nodes.Add(
                new DocNode(
                    namespacePage.Title,
                    namespacePage.Path,
                    namespacePage.Content.ToString()));
        }

        nodes.AddRange(stubNodes);

        return nodes;
    }

    /// <summary>
    /// Computes the method signature and safe ID for use in HTML content and stub nodes.
    /// </summary>
    /// <param name="method">The method declaration syntax.</param>
    /// <param name="qualifiedTypeName">The qualified name of the containing type.</param>
    /// <returns>A tuple containing the method signature and the safe ID.</returns>
    internal static (string Signature, string Id) GetMethodSignatureAndId(
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

    private static (string Signature, string Id) GetPropertySignatureAndId(
        PropertyDeclarationSyntax property,
        string qualifiedTypeName)
    {
        var signature = $"{property.Type} {property.Identifier.Text}{GetPropertyAccessorSignature(property)}";
        var id = StringUtils.ToSafeId($"{qualifiedTypeName}.{signature}");
        return (signature, id);
    }

    private static string GetHighlightedDisplaySignature(MethodDeclarationSyntax method)
    {
        var builder = new StringBuilder();
        var explicitInterface = method.ExplicitInterfaceSpecifier?.ToString().Trim();

        builder.Append($@"<span class=""sig-return"">{WebUtility.HtmlEncode(method.ReturnType.ToString())}</span> ");
        if (!string.IsNullOrEmpty(explicitInterface))
        {
            builder.Append($@"<span class=""sig-type"">{WebUtility.HtmlEncode(explicitInterface)}</span>");
        }

        builder.Append($@"<span class=""sig-method"">{WebUtility.HtmlEncode(method.Identifier.Text)}</span>");

        if (method.TypeParameterList is { Parameters.Count: > 0 } typeParams)
        {
            var typeParamDisplay = string.Join(", ", typeParams.Parameters.Select(p => p.Identifier.Text));
            builder.Append($@"<span class=""sig-generic"">&lt;{WebUtility.HtmlEncode(typeParamDisplay)}&gt;</span>");
        }

        builder.Append("(");
        var parameters = method.ParameterList.Parameters
            .Where(p => !IsCompilerGeneratedCallerParameter(p))
            .ToList();

        for (var i = 0; i < parameters.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            AppendHighlightedParameter(builder, parameters[i]);
        }

        builder.Append(")");

        return builder.ToString();
    }

    private static string GetHighlightedPropertySignature(PropertyDeclarationSyntax property)
    {
        var builder = new StringBuilder();
        builder.Append($@"<span class=""sig-type"">{WebUtility.HtmlEncode(property.Type.ToString())}</span> ");
        builder.Append($@"<span class=""sig-parameter"">{WebUtility.HtmlEncode(property.Identifier.Text)}</span>");

        var accessorSignature = GetPropertyAccessorSignature(property);
        if (!string.IsNullOrWhiteSpace(accessorSignature))
        {
            builder.Append(" ");
            builder.Append($@"<span class=""sig-operator"">{WebUtility.HtmlEncode(accessorSignature)}</span>");
        }

        return builder.ToString();
    }

    internal static string GetPropertyAccessorSignature(PropertyDeclarationSyntax property)
    {
        if (property.ExpressionBody != null)
        {
            return "{ get; }";
        }

        var accessors = property.AccessorList?.Accessors
            .Select(a => $"{a.Keyword.Text};")
            .ToList();

        if (accessors == null || accessors.Count == 0)
        {
            return string.Empty;
        }

        return "{ " + string.Join(" ", accessors) + " }";
    }

    internal static void AppendHighlightedParameter(StringBuilder builder, ParameterSyntax parameter)
    {
        var modifier = parameter.Modifiers.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(modifier))
        {
            builder.Append($@"<span class=""sig-modifier"">{WebUtility.HtmlEncode(modifier)}</span> ");
        }

        var type = parameter.Type?.ToString() ?? "object";
        builder.Append($@"<span class=""sig-type"">{WebUtility.HtmlEncode(type)}</span> ");
        builder.Append($@"<span class=""sig-parameter"">{WebUtility.HtmlEncode(parameter.Identifier.Text)}</span>");

        var defaultValue = parameter.Default?.Value.ToString();
        if (!string.IsNullOrWhiteSpace(defaultValue))
        {
            builder.Append(@" <span class=""sig-operator"">=</span> ");
            builder.Append($@"<span class=""sig-literal"">{WebUtility.HtmlEncode(defaultValue)}</span>");
        }
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

    private static bool IsCompilerGeneratedCallerParameter(ParameterSyntax parameter)
    {
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

        var encodedCssClass = WebUtility.HtmlEncode(cssClass);
        html.Append($"<div class=\"{encodedCssClass}\">");
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

        var encodedCssClass = WebUtility.HtmlEncode(cssClass);
        html.Append($"<div class=\"{encodedCssClass}\">");
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
        return WhitespaceRegex.Replace(value, " ");
    }

    internal static string? SimplifyCref(string? cref)
    {
        if (string.IsNullOrWhiteSpace(cref))
        {
            return null;
        }

        var simplified = cref.Trim();
        if (simplified.Length >= 2 && simplified[1] == ':')
        {
            simplified = simplified[2..];
        }

        return string.IsNullOrWhiteSpace(simplified) ? null : simplified;
    }

    private static bool IsCompilerGeneratedDocParameter(string? parameterName)
    {
        return parameterName is "callerFilePath" or "callerLineNumber";
    }

    internal static NamespaceDocPage GetOrCreateNamespacePage(
        IDictionary<string, NamespaceDocPage> namespacePages,
        string namespaceName)
    {
        var normalizedNamespace = string.IsNullOrWhiteSpace(namespaceName) ? "Global" : namespaceName.Trim();
        var path = BuildNamespaceDocPath(normalizedNamespace);

        if (!namespacePages.TryGetValue(path, out var page))
        {
            page = new NamespaceDocPage(normalizedNamespace, path, GetNamespaceTitle(normalizedNamespace));
            namespacePages[path] = page;
        }

        return page;
    }

    /// <summary>
    /// Ensures parent namespace pages and child links exist, then rebuilds <paramref name="namespacePages"/> in place keyed by <see cref="NamespaceDocPage.Path"/>.
    /// </summary>
    private static void EnsureNamespaceHierarchy(IDictionary<string, NamespaceDocPage> namespacePages)
    {
        var pagesByNamespace = namespacePages.Values
            .ToDictionary(p => p.FullNamespace, StringComparer.OrdinalIgnoreCase);

        if (!pagesByNamespace.ContainsKey(string.Empty))
        {
            pagesByNamespace[string.Empty] = new NamespaceDocPage(string.Empty, "Namespaces", "Namespaces");
        }

        var fullNamespaces = pagesByNamespace.Keys.Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
        foreach (var namespaceName in fullNamespaces)
        {
            var parts = namespaceName.Split('.', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 1; i < parts.Length; i++)
            {
                var parentNamespace = string.Join(".", parts.Take(i));
                if (!pagesByNamespace.ContainsKey(parentNamespace))
                {
                    pagesByNamespace[parentNamespace] = new NamespaceDocPage(
                        parentNamespace,
                        BuildNamespaceDocPath(parentNamespace),
                        GetNamespaceTitle(parentNamespace));
                }
            }
        }

        foreach (var page in pagesByNamespace.Values)
        {
            page.ChildNamespaces.Clear();
        }

        foreach (var namespaceName in pagesByNamespace.Keys.Where(k => !string.IsNullOrWhiteSpace(k)))
        {
            var parentNamespace = GetParentNamespace(namespaceName);
            if (pagesByNamespace.TryGetValue(parentNamespace, out var parentPage))
            {
                parentPage.ChildNamespaces.Add(namespaceName);
            }
        }

        foreach (var page in pagesByNamespace.Values)
        {
            if (page.ChildNamespaces.Count == 0)
            {
                continue;
            }

            var childLinks = page.ChildNamespaces
                .OrderBy(child => child, StringComparer.OrdinalIgnoreCase)
                .Select(
                    child =>
                    {
                        var childPath = BuildNamespaceDocPath(child);
                        var childTitle = GetNamespaceTitle(child);
                        return
                            $@"<li><a href=""/docs/{WebUtility.HtmlEncode(childPath)}.html"">{WebUtility.HtmlEncode(childTitle)}</a></li>";
                    });

            var childSection = new StringBuilder();
            childSection.Append("<section class=\"doc-namespace-groups\">");
            childSection.Append("<h4>Namespaces</h4>");
            childSection.Append("<ul>");
            foreach (var link in childLinks)
            {
                childSection.Append(link);
            }

            childSection.Append("</ul>");
            childSection.Append("</section>");

            page.Content.Insert(0, childSection.ToString());
        }

        namespacePages.Clear();
        foreach (var page in pagesByNamespace.Values)
        {
            namespacePages[page.Path] = page;
        }
    }

    private static string GetNamespaceName(SyntaxNode node)
    {
        var namespaceParts = node.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Select(n => n.Name.ToString())
            .Reverse()
            .ToList();

        if (namespaceParts.Count == 0)
        {
            return "Global";
        }

        return string.Join(".", namespaceParts);
    }

    internal static string BuildNamespaceDocPath(string namespaceName)
    {
        return string.IsNullOrWhiteSpace(namespaceName) ? "Namespaces" : $"Namespaces/{namespaceName}";
    }

    internal static string GetNamespaceTitle(string fullNamespace)
    {
        if (string.IsNullOrWhiteSpace(fullNamespace))
        {
            return "Namespaces";
        }

        var separatorIndex = fullNamespace.LastIndexOf('.');
        return separatorIndex >= 0 ? fullNamespace[(separatorIndex + 1)..] : fullNamespace;
    }

    private static string GetParentNamespace(string namespaceName)
    {
        var separatorIndex = namespaceName.LastIndexOf('.');
        return separatorIndex < 0 ? string.Empty : namespaceName[..separatorIndex];
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

    internal sealed class NamespaceDocPage
    {
        public NamespaceDocPage(string fullNamespace, string path, string title)
        {
            FullNamespace = fullNamespace;
            Title = title;
            Path = path;
        }

        public string FullNamespace { get; }

        public string Title { get; }

        public string Path { get; }

        public StringBuilder Content { get; } = new();

        public HashSet<string> ChildNamespaces { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
