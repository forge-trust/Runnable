using ForgeTrust.Runnable.Web.RazorDocs.Models;
using Markdig;

namespace ForgeTrust.Runnable.Web.RazorDocs.Services;

public class MarkdownHarvester : IDocHarvester
{
    private readonly MarkdownPipeline _pipeline;
    private readonly ILogger<MarkdownHarvester> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="MarkdownHarvester"/> using the provided logger and constructs a Markdown pipeline configured with advanced extensions.
    /// <summary>
    /// Initializes a new MarkdownHarvester and configures its Markdown processing pipeline.
    /// </summary>
    /// <param name="logger">Logger used to report errors and informational messages during harvesting.</param>
    public MarkdownHarvester(ILogger<MarkdownHarvester> logger)
    {
        _logger = logger;
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    /// <summary>
    /// Harvests Markdown files beneath the specified root directory and converts them into DocNode entries.
    /// </summary>
    /// <param name="rootPath">The root directory to search for `.md` files.</param>
    /// <summary>
    /// Harvests Markdown files under the specified root directory and converts each into a DocNode containing a display title, relative path, and generated HTML.
    /// </summary>
    /// <param name="rootPath">The root directory to search recursively for `.md` files.</param>
    /// <returns>A collection of DocNode objects representing each processed Markdown file, containing the display title, path relative to <paramref name="rootPath"/>, and generated HTML.</returns>
    /// <remarks>
    /// Skips files inside directories named "node_modules", "bin", or "obj". If a file's name is "README" (case-insensitive), its title is set to the parent directory name or "Home" for a repository root README. Files that fail to process are skipped and an error is logged.
    /// </remarks>
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