using ForgeTrust.Runnable.Web.RazorDocs.Models;
using Markdig;

namespace ForgeTrust.Runnable.Web.RazorDocs.Services;

/// <summary>
/// Harvester implementation that scans Markdown source files and converts them into documentation nodes.
/// </summary>
public class MarkdownHarvester : IDocHarvester
{
    private readonly MarkdownPipeline _pipeline;
    private readonly ILogger<MarkdownHarvester> _logger;
    private readonly Func<string, CancellationToken, Task<string>> _readAllTextAsync;

    /// <summary>
    /// Initializes a new instance of <see cref="MarkdownHarvester"/> with the specified logger and configures the Markdown pipeline.
    /// </summary>
    /// <param name="logger">Logger used for recording harvesting events and errors.</param>
    public MarkdownHarvester(ILogger<MarkdownHarvester> logger)
        : this(logger, File.ReadAllTextAsync)
    {
    }

    internal MarkdownHarvester(
        ILogger<MarkdownHarvester> logger,
        Func<string, CancellationToken, Task<string>> readAllTextAsync)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(readAllTextAsync);

        _logger = logger;
        _readAllTextAsync = readAllTextAsync;
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
    /// Skips files in excluded directories (for example "node_modules", "bin", "obj", and "Tests") and hidden dot-prefixed directories unless explicitly allowlisted. Dot-prefixed files are included. If a file's name is "README" (case-insensitive), its title is set to the parent directory name or "Home" for a repository root README. Files that fail to process are skipped and an error is logged.
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
                if (HarvestPathExclusions.ShouldExcludeFilePath(relativePath))
                {
                    continue;
                }

                var content = await _readAllTextAsync(file, cancellationToken);
                var (markdownBody, frontMatterMetadata) = MarkdownFrontMatterParser.Extract(content);
                var html = Markdown.ToHtml(markdownBody, _pipeline);
                var title = Path.GetFileNameWithoutExtension(file);

                if (title.Equals("README", StringComparison.OrdinalIgnoreCase))
                {
                    var parentDir = Path.GetDirectoryName(relativePath);
                    title = string.IsNullOrEmpty(parentDir) ? "Home" : Path.GetFileName(parentDir);
                }

                var resolvedTitle = frontMatterMetadata?.Title ?? title;
                var metadata = DocMetadataFactory.CreateMarkdownMetadata(
                    relativePath,
                    resolvedTitle,
                    frontMatterMetadata,
                    ExtractSummary(markdownBody));

                nodes.Add(new DocNode(resolvedTitle, relativePath, html, Metadata: metadata));
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

    internal static string? ExtractSummary(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return null;
        }

        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var summaryLines = new List<string>();
        var inCodeFence = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                inCodeFence = !inCodeFence;
                continue;
            }

            if (inCodeFence || string.IsNullOrWhiteSpace(trimmed))
            {
                if (summaryLines.Count > 0)
                {
                    break;
                }

                continue;
            }

            if (trimmed.StartsWith("#", StringComparison.Ordinal)
                || trimmed.StartsWith("- ", StringComparison.Ordinal)
                || trimmed.StartsWith("* ", StringComparison.Ordinal)
                || StartsWithNumberedListMarker(trimmed)
                || trimmed.StartsWith("> ", StringComparison.Ordinal)
                || trimmed.StartsWith("<!--", StringComparison.Ordinal))
            {
                if (summaryLines.Count > 0)
                {
                    break;
                }

                continue;
            }

            summaryLines.Add(trimmed);
        }

        return summaryLines.Count == 0 ? null : string.Join(" ", summaryLines);
    }

    private static bool StartsWithNumberedListMarker(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !char.IsDigit(value[0]))
        {
            return false;
        }

        var index = 0;
        while (index < value.Length && char.IsDigit(value[index]))
        {
            index++;
        }

        return index + 1 < value.Length
               && value[index] == '.'
               && value[index + 1] == ' ';
    }
}
