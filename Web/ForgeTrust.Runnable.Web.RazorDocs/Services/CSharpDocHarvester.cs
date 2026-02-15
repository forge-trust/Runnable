using System.Net;
using System.Text;
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
    /// <remarks>
    /// Skips files in excluded directories (for example "node_modules", "bin", "obj", and "Tests") and hidden dot-prefixed directories unless explicitly allowlisted. Dot-prefixed files are included.
    /// </remarks>
    public async Task<IEnumerable<DocNode>> HarvestAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        var nodes = new List<DocNode>();
        var csFiles = Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories);

        foreach (var file in csFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(rootPath, file)
                .Replace('\\', '/'); // Normalize to forward slashes for URLs
            if (HarvestPathExclusions.ShouldExcludeFilePath(relativePath))
            {
                continue;
            }

            try
            {
                var code = await File.ReadAllTextAsync(file, cancellationToken);
                var tree = CSharpSyntaxTree.ParseText(code, cancellationToken: cancellationToken);
                var root = await tree.GetRootAsync(cancellationToken);

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
                    if (doc != null)
                    {
                        hasAnyDoc = true;
                        var qualifiedName = GetQualifiedName(typeDecl);
                        var typeId = StringUtils.ToSafeId(qualifiedName);

                        fileContent.Append(
                            $@"<section id=""{typeId}"" class=""mb-12 scroll-mt-24"">
                            <div class=""flex items-center gap-2 mb-4"">
                                <span class=""px-2 py-0.5 rounded bg-blue-500/10 text-blue-400 border border-blue-500/20 text-[10px] font-bold uppercase tracking-wider"">Type</span>
                                <h2 class=""text-2xl font-bold text-white"">{WebUtility.HtmlEncode(typeDecl.Identifier.Text)}</h2>
                            </div>
                            <div class=""pl-4 border-l-2 border-slate-800"">
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
                                    typeDecl.Identifier.Text,
                                    relativePath + "#" + typeId,
                                    string.Empty,
                                    relativePath));
                        }
                    }

                    var methods = typeDecl.Members.OfType<MethodDeclarationSyntax>().ToList();
                    foreach (var method in methods)
                    {
                        var methodDoc = ExtractDoc(method);
                        if (methodDoc != null)
                        {
                            hasAnyDoc = true;
                            var qualifiedTypeName = GetQualifiedName(typeDecl);
                            var (signature, id) = GetMethodSignatureAndId(method, qualifiedTypeName);

                            fileContent.Append(
                                $@"<section id=""{id}"" class=""mb-8 ml-6 scroll-mt-24"">
                                <div class=""flex items-center gap-2 mb-2"">
                                    <span class=""px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 text-[10px] font-bold uppercase tracking-wider"">Method</span>
                                    <h3 class=""text-lg font-semibold text-slate-200"">{WebUtility.HtmlEncode(signature)}</h3>
                                </div>
                                <div class=""pl-4 border-l-2 border-slate-800/50"">
                                    {methodDoc}
                                </div>
                            </section>");

                            // Add method stub
                            stubNodes.Add(
                                new DocNode(
                                    signature,
                                    relativePath + "#" + id,
                                    string.Empty,
                                    relativePath));
                        }
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
                            $@"<section id=""{enumId}"" class=""mb-12 scroll-mt-24"">
                            <div class=""flex items-center gap-2 mb-4"">
                                <span class=""px-2 py-0.5 rounded bg-amber-500/10 text-amber-400 border border-amber-500/20 text-[10px] font-bold uppercase tracking-wider"">Enum</span>
                                <h2 class=""text-2xl font-bold text-white"">{WebUtility.HtmlEncode(enumDecl.Identifier.Text)}</h2>
                            </div>
                            <div class=""pl-4 border-l-2 border-slate-800"">
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

    /// <summary>
    /// Extracts XML documentation from the leading trivia of a syntax node and converts the <c>&lt;summary&gt;</c> and <c>&lt;remarks&gt;</c> elements into HTML fragments.
    /// </summary>
    /// <param name="node">The syntax node whose leading XML documentation comments will be parsed.</param>
    /// <returns>The HTML string containing encoded summary and remarks, or <c>null</c> if no documentation is present or parsing fails.</returns>
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
            var xdoc = XDocument.Parse(wrappedXml);

            var summary = xdoc.Root?.Element("summary")?.Value.Trim();
            var remarks = xdoc.Root?.Element("remarks")?.Value.Trim();

            var html = "";
            if (!string.IsNullOrEmpty(summary))
            {
                html += $"<div class='doc-summary text-slate-300 mb-4'>{WebUtility.HtmlEncode(summary)}</div>";
            }

            if (!string.IsNullOrEmpty(remarks))
            {
                html += $"<div class='doc-remarks text-slate-400 italic'>{WebUtility.HtmlEncode(remarks)}</div>";
            }

            return string.IsNullOrEmpty(html) ? null : html;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse XML documentation for node {Node}", node.ToString().Split('\n')[0]);

            return null;
        }
    }

    /// <summary>
    /// Builds the dot-delimited qualified name for a type or enum declaration, including enclosing types and namespaces.
    /// </summary>
    /// <param name="node">The type or enum declaration syntax node to compute the qualified name for.</param>
    /// <returns>The qualified name as a dot-delimited string containing nested type and namespace segments.</returns>
    private string GetQualifiedName(BaseTypeDeclarationSyntax node)
    {
        var parts = new Stack<string>();
        parts.Push(node.Identifier.Text);

        var parent = node.Parent;
        while (parent != null)
        {
            if (parent is TypeDeclarationSyntax typeDecl)
            {
                parts.Push(typeDecl.Identifier.Text);
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
