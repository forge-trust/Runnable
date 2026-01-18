using System.Text.RegularExpressions;
using CliFx.Infrastructure;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

public class ExportEngine
{
    private readonly string _projectPath;
    private readonly string _outputPath;
    private readonly string _mode;
    private readonly string? _seedRoutesPath;
    private readonly string _baseUrl;
    private readonly IConsole _console;
    private readonly HttpClient _client = new();
    private readonly HashSet<string> _visited = new();
    private readonly Queue<string> _queue = new();

    public ExportEngine(
        string projectPath,
        string outputPath,
        string mode,
        string? seedRoutesPath,
        string baseUrl,
        IConsole console)
    {
        _projectPath = projectPath;
        _outputPath = outputPath;
        _mode = mode;
        _seedRoutesPath = seedRoutesPath;
        _baseUrl = baseUrl.TrimEnd('/');
        _console = console;
    }

    public async Task RunAsync()
    {
        // 1. Seed routes
        if (_seedRoutesPath != null && File.Exists(_seedRoutesPath))
        {
            var seeds = await File.ReadAllLinesAsync(_seedRoutesPath);
            foreach (var seed in seeds)
            {
                var trimmed = seed.Trim();
                if (!string.IsNullOrEmpty(trimmed)) _queue.Enqueue(trimmed);
            }
        }
        else
        {
            _queue.Enqueue("/");
        }

        _console.Output.WriteLine($"Crawling from {_baseUrl}...");

        while (_queue.Count > 0)
        {
            var route = _queue.Dequeue();

            if (_visited.Contains(route)) continue;
            _visited.Add(route);

            await ExportRouteAsync(route);
        }
    }

    private async Task ExportRouteAsync(string route)
    {
        _console.Output.WriteLine($"  -> {route}");

        try
        {
            var response = await _client.GetAsync($"{_baseUrl}{route}");
            if (!response.IsSuccessStatusCode)
            {
                _console.Error.WriteLine($"  !! Failed to fetch {route}: {response.StatusCode}");

                return;
            }

            var html = await response.Content.ReadAsStringAsync();

            // Save file
            var filePath = MapRouteToFilePath(route);
            var dirPath = Path.GetDirectoryName(filePath);
            if (dirPath != null && !Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);
            await File.WriteAllTextAsync(filePath, html);

            // Extract links and frames
            ExtractLinks(html);
            ExtractFrames(html);
        }
        catch (Exception ex)
        {
            _console.Error.WriteLine($"  !! Error exporting {route}: {ex.Message}");
        }
    }

    private string MapRouteToFilePath(string route)
    {
        var normalized = route == "/" ? "/index" : route;
        if (!normalized.EndsWith("/index") && !normalized.EndsWith(".html"))
        {
            normalized = normalized.TrimEnd('/') + "/index";
        }

        var relativePath = normalized.TrimStart('/').Replace('/', Path.DirectorySeparatorChar) + ".html";

        return Path.Combine(_outputPath, relativePath);
    }

    private void ExtractLinks(string html)
    {
        var matches = Regex.Matches(html, "href=\"([^\"]+)\"");
        foreach (Match match in matches)
        {
            var href = match.Groups[1].Value;
            if (href.StartsWith("/")
                && !href.StartsWith("//")
                && !href.Contains(":")
                && !href.Contains("#")
                && !_visited.Contains(href))
            {
                _queue.Enqueue(href);
            }
        }
    }

    private void ExtractFrames(string html)
    {
        var matches = Regex.Matches(html, "<turbo-frame [^>]*src=\"([^\"]+)\"");
        foreach (Match match in matches)
        {
            var src = match.Groups[1].Value;
            if (src.StartsWith("/")
                && !src.StartsWith("//")
                && !src.Contains(":")
                && !src.Contains("#")
                && !_visited.Contains(src))
            {
                _queue.Enqueue(src);
            }
        }
    }
}
