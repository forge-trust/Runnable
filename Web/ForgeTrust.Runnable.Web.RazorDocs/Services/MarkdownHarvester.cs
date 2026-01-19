using ForgeTrust.Runnable.Web.RazorDocs.Models;
using Markdig;

namespace ForgeTrust.Runnable.Web.RazorDocs.Services;

public class MarkdownHarvester : IDocHarvester
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownHarvester()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public async Task<IEnumerable<DocNode>> HarvestAsync(string rootPath)
    {
        var nodes = new List<DocNode>();
        var mdFiles = Directory.EnumerateFiles(rootPath, "*.md", SearchOption.AllDirectories);

        foreach (var file in mdFiles)
        {
            if (file.Contains("node_modules") || file.Contains("bin") || file.Contains("obj"))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(file);
            var html = Markdown.ToHtml(content, _pipeline);
            var relativePath = Path.GetRelativePath(rootPath, file);
            var title = Path.GetFileNameWithoutExtension(file);

            if (title.Equals("README", StringComparison.OrdinalIgnoreCase))
            {
                var parentDir = Path.GetDirectoryName(relativePath);
                title = string.IsNullOrEmpty(parentDir) ? "Home" : Path.GetFileName(parentDir);
            }

            nodes.Add(new DocNode(title, relativePath, html));
        }

        return nodes;
    }
}
