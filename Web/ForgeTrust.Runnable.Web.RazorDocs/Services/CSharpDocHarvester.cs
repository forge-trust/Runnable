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
                var relativePath = Path.GetRelativePath(rootPath, file);

                var fileContent = new StringBuilder();
                var hasAnyDoc = false;

                // Cache ExtractDoc results to avoid duplicate XML parsing
                var typeDocs = new Dictionary<TypeDeclarationSyntax, string?>();
                var methodDocs = new Dictionary<MethodDeclarationSyntax, string?>();
                var enumDocs = new Dictionary<EnumDeclarationSyntax, string?>();

                // Capture Classes, Structs, Interfaces, Records
                var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>().ToList();
                foreach (var typeDecl in typeDeclarations)
                {
                    var doc = ExtractDoc(typeDecl);
                    typeDocs[typeDecl] = doc;
                    if (doc != null)
                    {
                        hasAnyDoc = true;
                        var qualifiedName = GetQualifiedName(typeDecl);
                        // Use qualified name for ID to avoid collisions (e.g. NamespaceA.Class vs NamespaceB.Class)
                        var typeId = StringUtils.ToSafeId(qualifiedName);

                        fileContent.Append(
                            $@"<section id=""{typeId}"" class=""mb-12 scroll-mt-24"">
                            <div class=""flex items-center gap-2 mb-4"">
                                <span class=""px-2 py-0.5 rounded bg-blue-500/10 text-blue-400 border border-blue-500/20 text-[10px] font-bold uppercase tracking-wider"">Type</span>
                                <h2 class=""text-2xl font-bold text-white"">{typeDecl.Identifier.Text}</h2>
                            </div>
                            <div class=""pl-4 border-l-2 border-slate-800"">
                                {doc}
                            </div>
                        </section>");
                    }

                    var methods = typeDecl.Members.OfType<MethodDeclarationSyntax>().ToList();
                    foreach (var method in methods)
                    {
                        var methodDoc = ExtractDoc(method);
                        methodDocs[method] = methodDoc;
                        if (methodDoc != null)
                        {
                            hasAnyDoc = true;
                            var paramList = string.Join(
                                ", ",
                                method.ParameterList.Parameters.Select(p =>
                                    $"{p.Modifiers.ToString().Trim()} {p.Type?.ToString() ?? "object"}".Trim()));

                            // From main: Include type parameters and explicit interface
                            var typeParams = method.TypeParameterList?.ToString().Trim() ?? "";
                            var explicitInterface = method.ExplicitInterfaceSpecifier?.ToString().Trim() ?? "";
                            var methodName = explicitInterface + method.Identifier.Text + typeParams;
                            var methodSignature = $"{methodName}({paramList})";

                            var qualifiedName = GetQualifiedName(typeDecl);
                            // Use qualified name in method ID as well
                            var methodId = StringUtils.ToSafeId(
                                $"{qualifiedName}.{methodSignature}");

                            fileContent.Append(
                                $@"<section id=""{methodId}"" class=""mb-8 ml-6 scroll-mt-24"">
                                <div class=""flex items-center gap-2 mb-2"">
                                    <span class=""px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 text-[10px] font-bold uppercase tracking-wider"">Method</span>
                                    <h3 class=""text-lg font-semibold text-slate-200"">{methodSignature}</h3>
                                </div>
                                <div class=""pl-4 border-l-2 border-slate-800/50"">
                                    {methodDoc}
                                </div>
                            </section>");
                        }
                    }
                }

                // Capture Enums
                var enumDeclarations = root.DescendantNodes().OfType<EnumDeclarationSyntax>().ToList();
                foreach (var enumDecl in enumDeclarations)
                {
                    var doc = ExtractDoc(enumDecl);
                    enumDocs[enumDecl] = doc;
                    if (doc != null)
                    {
                        hasAnyDoc = true;
                        var qualifiedName = GetQualifiedName(enumDecl);
                        var enumId = StringUtils.ToSafeId(qualifiedName);

                        fileContent.Append(
                            $@"<section id=""{enumId}"" class=""mb-12 scroll-mt-24"">
                            <div class=""flex items-center gap-2 mb-4"">
                                <span class=""px-2 py-0.5 rounded bg-amber-500/10 text-amber-400 border border-amber-500/20 text-[10px] font-bold uppercase tracking-wider"">Enum</span>
                                <h2 class=""text-2xl font-bold text-white"">{enumDecl.Identifier.Text}</h2>
                            </div>
                            <div class=""pl-4 border-l-2 border-slate-800"">
                                {doc}
                            </div>
                        </section>");
                    }
                }

                if (hasAnyDoc)
                {
                    var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);

                    // Add the main file node
                    var fileNode = new DocNode(
                        fileNameWithoutExt,
                        relativePath,
                        fileContent.ToString()
                    );
                    nodes.Add(fileNode);

                    // Add member-level nodes for the sidebar (navigation stubs)
                    foreach (var typeDecl in typeDeclarations)
                    {
                        // Add type stub if documented and type name doesn't match filename
                        // Use cached doc result from content generation pass
                        if (typeDocs.GetValueOrDefault(typeDecl) != null
                            && !string.Equals(
                                typeDecl.Identifier.Text,
                                fileNameWithoutExt,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            var qualifiedName = GetQualifiedName(typeDecl);
                            var typeId = StringUtils.ToSafeId(qualifiedName);
                            nodes.Add(
                                new DocNode(
                                    typeDecl.Identifier.Text,
                                    relativePath + "#" + typeId,
                                    string.Empty,
                                    relativePath
                                ));
                        }

                        // Add method stubs
                        var methods = typeDecl.Members.OfType<MethodDeclarationSyntax>().ToList();
                        foreach (var method in methods)
                        {
                            // Use cached doc result from content generation pass
                            if (methodDocs.GetValueOrDefault(method) != null)
                            {
                                var paramList = string.Join(
                                    ", ",
                                    method.ParameterList.Parameters.Select(p =>
                                        $"{p.Modifiers.ToString().Trim()} {p.Type?.ToString() ?? "object"}".Trim()));

                                var typeParams = method.TypeParameterList?.ToString().Trim() ?? "";
                                var explicitInterface = method.ExplicitInterfaceSpecifier?.ToString().Trim() ?? "";
                                var methodName = explicitInterface + method.Identifier.Text + typeParams;
                                var methodSignature = $"{methodName}({paramList})";

                                var qualifiedName = GetQualifiedName(typeDecl);
                                var methodId = StringUtils.ToSafeId(
                                    $"{qualifiedName}.{methodSignature}");

                                nodes.Add(
                                    new DocNode(
                                        methodSignature,
                                        relativePath + "#" + methodId,
                                        string.Empty,
                                        relativePath
                                    ));
                            }
                        }
                    }

                    foreach (var enumDecl in enumDeclarations)
                    {
                        // Add enum stub if documented and enum name doesn't match filename
                        // Use cached doc result from content generation pass
                        if (enumDocs.GetValueOrDefault(enumDecl) != null
                            && !string.Equals(
                                enumDecl.Identifier.Text,
                                fileNameWithoutExt,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            var qualifiedName = GetQualifiedName(enumDecl);
                            var enumId = StringUtils.ToSafeId(qualifiedName);
                            nodes.Add(
                                new DocNode(
                                    enumDecl.Identifier.Text,
                                    relativePath + "#" + enumId,
                                    string.Empty,
                                    relativePath
                                ));
                        }
                    }
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