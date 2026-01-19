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
                var relativePath = Path.GetRelativePath(rootPath, file);

                var fileContent = new StringBuilder();
                var hasAnyDoc = false;

                // Capture Classes, Structs, Interfaces, Records
                var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>().ToList();
                foreach (var typeDecl in typeDeclarations)
                {
                    var doc = ExtractDoc(typeDecl);
                    if (doc != null)
                    {
                        hasAnyDoc = true;
                        var typeId = StringUtils.ToSafeId(typeDecl.Identifier.Text);
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
                        if (methodDoc != null)
                        {
                            hasAnyDoc = true;
                            var paramList = string.Join(
                                ", ",
                                method.ParameterList.Parameters.Select(p =>
                                    $"{p.Modifiers.ToString().Trim()} {p.Type?.ToString() ?? "object"}".Trim()));

                            var methodSignature = $"{method.Identifier.Text}({paramList})";
                            var methodId = StringUtils.ToSafeId(
                                $"{typeDecl.Identifier.Text}.{method.Identifier.Text}({paramList})");

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
                    if (doc != null)
                    {
                        hasAnyDoc = true;
                        var enumId = StringUtils.ToSafeId(enumDecl.Identifier.Text);
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
                    // Add the main file node
                    var fileNode = new DocNode(
                        Path.GetFileName(file),
                        relativePath,
                        fileContent.ToString()
                    );
                    nodes.Add(fileNode);

                    // Add member-level nodes for the sidebar (navigation stubs)
                    foreach (var typeDecl in typeDeclarations)
                    {
                        // Add type stub if documented
                        if (ExtractDoc(typeDecl) != null)
                        {
                            var typeId = StringUtils.ToSafeId(typeDecl.Identifier.Text);
                            nodes.Add(
                                new DocNode(
                                    typeDecl.Identifier.Text,
                                    relativePath + "#" + typeId,
                                    string.Empty,
                                    relativePath
                                ));
                        }

                        // Add method stubs if documented (independent of whether type has docs)
                        var methods = typeDecl.Members.OfType<MethodDeclarationSyntax>().ToList();
                        foreach (var method in methods)
                        {
                            if (ExtractDoc(method) != null)
                            {
                                var paramList = string.Join(
                                    ", ",
                                    method.ParameterList.Parameters.Select(p =>
                                        $"{p.Modifiers.ToString().Trim()} {p.Type?.ToString() ?? "object"}".Trim()));

                                var methodSignature = $"{method.Identifier.Text}({paramList})";
                                var methodId = StringUtils.ToSafeId(
                                    $"{typeDecl.Identifier.Text}.{method.Identifier.Text}({paramList})");

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
                        if (ExtractDoc(enumDecl) != null)
                        {
                            var enumId = StringUtils.ToSafeId(enumDecl.Identifier.Text);
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
}
