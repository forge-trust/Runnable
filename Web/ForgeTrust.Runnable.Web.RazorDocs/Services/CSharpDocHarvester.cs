using System.Net;
using System.Xml.Linq;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ForgeTrust.Runnable.Web.RazorDocs.Services;

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
                        nodes.Add(
                            new DocNode(
                                typeDecl.Identifier.Text,
                                Path.GetRelativePath(rootPath, file) + "#" + typeDecl.Identifier.Text,
                                doc
                            ));
                    }

                    var methods = typeDecl.DescendantNodes().OfType<MethodDeclarationSyntax>();
                    foreach (var method in methods)
                    {
                        var methodDoc = ExtractDoc(method);
                        if (methodDoc != null)
                        {
                            nodes.Add(
                                new DocNode(
                                    $"{typeDecl.Identifier.Text}.{method.Identifier.Text}",
                                    Path.GetRelativePath(rootPath, file)
                                    + "#"
                                    + typeDecl.Identifier.Text
                                    + "."
                                    + method.Identifier.Text,
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
                        nodes.Add(
                            new DocNode(
                                @enum.Identifier.Text,
                                Path.GetRelativePath(rootPath, file) + "#" + @enum.Identifier.Text,
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
                html += $"<div class='doc-summary text-slate-600 mb-4'>{WebUtility.HtmlEncode(summary)}</div>";
            }

            if (!string.IsNullOrEmpty(remarks))
            {
                html += $"<div class='doc-remarks text-slate-500 italic'>{WebUtility.HtmlEncode(remarks)}</div>";
            }

            return string.IsNullOrEmpty(html) ? null : html;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse XML documentation for node {Node}", node.ToString().Split('\n')[0]);

            return null;
        }
    }
}
