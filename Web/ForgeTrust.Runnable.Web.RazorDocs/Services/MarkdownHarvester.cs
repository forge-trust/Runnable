using ForgeTrust.Runnable.Web.RazorDocs.Models;
using Markdig;

namespace ForgeTrust.Runnable.Web.RazorDocs.Services;

public class MarkdownHarvester : IDocHarvester
{
    private readonly MarkdownPipeline _pipeline;
    private readonly ILogger<MarkdownHarvester> _logger;

    public MarkdownHarvester(ILogger<MarkdownHarvester> logger)
    {
        _logger = logger;
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public async Task<IEnumerable<DocNode>> HarvestAsync(string rootPath)
    {
        var nodes = new List<DocNode>();
        var mdFiles = Directory.EnumerateFiles(rootPath, "*.md", SearchOption.AllDirectories);
        var excludedDirs = new[] { "node_modules", "bin", "obj" };

        foreach (var file in mdFiles)
        {
            var segments = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Any(s => excludedDirs.Contains(s, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            try
            {
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process markdown file: {File}", file);
            }
        }

        return nodes;
    }
}
