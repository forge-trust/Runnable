using ForgeTrust.Runnable.Web.RazorDocs.Models;
using Markdig;
using System.Diagnostics.CodeAnalysis;

namespace ForgeTrust.Runnable.Web.RazorDocs.Services;

/// <summary>
/// Harvester implementation that scans Markdown source files and converts them into documentation nodes.
/// </summary>
[ExcludeFromCodeCoverage]
public class MarkdownHarvester : IDocHarvester
{
    private readonly MarkdownPipeline _pipeline;
    private readonly ILogger<MarkdownHarvester> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="MarkdownHarvester"/> with the specified logger and configures the Markdown pipeline.
    /// </summary>
    /// <param name="logger">Logger used for recording harvesting events and errors.</param>
    public MarkdownHarvester(ILogger<MarkdownHarvester> logger)
    {
        _logger = logger;
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    /// <summary>
    /// Harvests Markdown files under the specified root directory and converts each into a DocNode containing a display title, relative path, and generated HTML.
    /// </summary>
    /// <param name="rootPath">The root directory to search recursively for `.md` files.</param>
    /// <param name="cancellationToken">An optional token to observe for cancellation requests.</param>
    /// <returns>A collection of DocNode objects representing each processed Markdown file, containing the display title, path relative to <paramref name="rootPath"/>, and generated HTML.</returns>
    /// <remarks>
    /// Skips files in excluded directories (for example "node_modules", "bin", "obj") and hidden dot-prefixed directories unless explicitly allowlisted. If a file's name is "README" (case-insensitive), its title is set to the parent directory name or "Home" for a repository root README. Files that fail to process are skipped and an error is logged.
    /// </remarks>
    public async Task<IEnumerable<DocNode>> HarvestAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        var nodes = new List<DocNode>();
        var mdFiles = Directory.EnumerateFiles(rootPath, "*.md", SearchOption.AllDirectories);

        foreach (var file in mdFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var relativePath = Path.GetRelativePath(rootPath, file).Replace('\\', '/');
                if (HarvestPathExclusions.ShouldExclude(relativePath))
                {
                    continue;
                }

                var content = await File.ReadAllTextAsync(file, cancellationToken);
                var html = Markdown.ToHtml(content, _pipeline);
                var title = Path.GetFileNameWithoutExtension(file);

                if (title.Equals("README", StringComparison.OrdinalIgnoreCase))
                {
                    var parentDir = Path.GetDirectoryName(relativePath);
                    title = string.IsNullOrEmpty(parentDir) ? "Home" : Path.GetFileName(parentDir);
                }

                nodes.Add(new DocNode(title, relativePath, html));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process markdown file: {File}", file);
            }
        }

        return nodes;
    }
}
