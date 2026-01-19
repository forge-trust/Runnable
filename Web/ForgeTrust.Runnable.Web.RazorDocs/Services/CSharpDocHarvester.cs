using System.Xml.Linq;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ForgeTrust.Runnable.Web.RazorDocs.Services;

public class CSharpDocHarvester : IDocHarvester
{
    public async Task<IEnumerable<DocNode>> HarvestAsync(string rootPath)
    {
        var nodes = new List<DocNode>();
        var csFiles = Directory.EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories);

        foreach (var file in csFiles)
        {
            if (file.Contains("node_modules") || file.Contains("bin") || file.Contains("obj") || file.Contains("Tests"))
            {
                continue;
            }

            var code = await File.ReadAllTextAsync(file);
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = await tree.GetRootAsync();

            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
            foreach (var @class in classes)
            {
                var doc = ExtractDoc(@class);
                if (doc != null)
                {
                    nodes.Add(
                        new DocNode(
                            @class.Identifier.Text,
                            Path.GetRelativePath(rootPath, file) + "#" + @class.Identifier.Text,
                            doc
                        ));
                }

                var methods = @class.DescendantNodes().OfType<MethodDeclarationSyntax>();
                foreach (var method in methods)
                {
                    var methodDoc = ExtractDoc(method);
                    if (methodDoc != null)
                    {
                        nodes.Add(
                            new DocNode(
                                $"{@class.Identifier.Text}.{method.Identifier.Text}",
                                Path.GetRelativePath(rootPath, file)
                                + "#"
                                + @class.Identifier.Text
                                + "."
                                + method.Identifier.Text,
                                methodDoc
                            ));
                    }
                }
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

        // Simple transformation of XML doc to HTML
        // In a real app, we'd use a more robust XML -> HTML converter
        try
        {
            var cleanXml = xml.ToString().Replace("///", "").Trim();
            // Wrap in a root element to make it valid XML if it isn't already
            var wrappedXml = $"<doc>{cleanXml}</doc>";
            var xdoc = XDocument.Parse(wrappedXml);

            var summary = xdoc.Root?.Element("summary")?.Value.Trim();
            var remarks = xdoc.Root?.Element("remarks")?.Value.Trim();

            var html = "";
            if (!string.IsNullOrEmpty(summary)) html += $"<div class='doc-summary text-slate-600 mb-4'>{summary}</div>";
            if (!string.IsNullOrEmpty(remarks))
                html += $"<div class='doc-remarks text-slate-500 italic'>{remarks}</div>";

            return string.IsNullOrEmpty(html) ? null : html;
        }
        catch
        {
            return null;
        }
    }
}
