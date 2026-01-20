using System.Net;
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

    public CSharpDocHarvester(ILogger<CSharpDocHarvester> logger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<DocNode>> HarvestAsync(string rootPath)
    {
        var nodes = new List<DocNode>();
        var csFiles = Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories);
        var excludedDirs = new[] { "node_modules", "bin", "obj", "Tests" };

        foreach (var file in csFiles)
        {
            var segments = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Any(s => excludedDirs.Contains(s, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            try
            {
                var code = await File.ReadAllTextAsync(file);
                var tree = CSharpSyntaxTree.ParseText(code);
                var root = await tree.GetRootAsync();

                // Capture Classes, Structs, Interfaces, Records
                var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>();
                foreach (var typeDecl in typeDeclarations)
                {
                    var doc = ExtractDoc(typeDecl);
                    if (doc != null)
                    {
                        var qualifiedName = GetQualifiedName(typeDecl);
                        var safeAnchor = StringUtils.ToSafeId(qualifiedName);

                        nodes.Add(
                            new DocNode(
                                typeDecl.Identifier.Text,
                                Path.GetRelativePath(rootPath, file) + "#" + safeAnchor,
                                doc
                            ));
                    }

                    var methods = typeDecl.Members.OfType<MethodDeclarationSyntax>();
                    foreach (var method in methods)
                    {
                        var methodDoc = ExtractDoc(method);
                        if (methodDoc != null)
                        {
                            var paramList = string.Join(
                                ", ",
                                method.ParameterList.Parameters.Select(p =>
                                    $"{p.Modifiers.ToString().Trim()} {p.Type?.ToString() ?? "object"}".Trim()));

                            // Readable signature for the UI Title: e.g. "MyMethod(int, string)"
                            var readableSignature = $"({paramList})";
                            var methodId = $"{typeDecl.Identifier.Text}.{method.Identifier.Text}{readableSignature}";

                            // Sanitized signature for the Anchor/ID: e.g. "MyMethod-int-string-"
                            var anchor = StringUtils.ToSafeId(methodId);

                            nodes.Add(
                                new DocNode(
                                    methodId, // Title is now readable
                                    Path.GetRelativePath(rootPath, file) + "#" + anchor, // Path anchor is safe
                                    methodDoc
                                ));
                        }
                    }
                }

                // Capture Enums
                var enums = root.DescendantNodes().OfType<EnumDeclarationSyntax>();
                foreach (var @enum in enums)
                {
                    var doc = ExtractDoc(@enum);
                    if (doc != null)
                    {
                        var qualifiedName = GetQualifiedName(@enum);
                        var safeAnchor = StringUtils.ToSafeId(qualifiedName);

                        nodes.Add(
                            new DocNode(
                                @enum.Identifier.Text,
                                Path.GetRelativePath(rootPath, file) + "#" + safeAnchor,
                                doc
                            ));
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
    /// Extracts XML documentation from the leading trivia of a given syntax node and converts it to HTML.
    /// </summary>
    /// <param name="node">The syntax node to extract documentation from.</param>
    /// <returns>The generated HTML string if documentation exists; otherwise, null.</returns>
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
    /// Builds a qualified name for a type or enum by walking up the syntax tree and prepending containing type and namespace names.
    /// </summary>
    /// <param name="node">The type or enum declaration node.</param>
    /// <returns>A dot-delimited qualified name string.</returns>
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
