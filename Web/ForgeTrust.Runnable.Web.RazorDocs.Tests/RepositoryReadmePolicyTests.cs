namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public sealed class RepositoryReadmePolicyTests
{
    private static readonly HashSet<string> ExcludedDirectorySegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        "node_modules",
        "TestResults"
    };

    [Fact]
    public void AuthoredReadmes_ShouldNotStartWithYamlFrontMatter()
    {
        var repoRoot = TestPathUtils.FindRepoRoot(AppContext.BaseDirectory);
        var violatingReadmes = EnumerateAuthoredReadmePaths(repoRoot)
            .Where(
                path => File.ReadAllText(Path.Combine(repoRoot, path))
                    .Replace("\r\n", "\n", StringComparison.Ordinal)
                    .StartsWith("---\n", StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            violatingReadmes.Length == 0,
            $"Authored README.md files must stay portable and avoid inline YAML front matter. Violations: {string.Join(", ", violatingReadmes)}");
    }

    private static IReadOnlyList<string> EnumerateAuthoredReadmePaths(string repoRoot)
    {
        return Directory
            .EnumerateFiles(repoRoot, "README.md", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(repoRoot, path).Replace('\\', '/'))
            .Where(path => !ShouldExcludePath(path))
            .ToArray();
    }

    private static bool ShouldExcludePath(string relativePath)
    {
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= 1)
        {
            return false;
        }

        foreach (var directorySegment in segments[..^1])
        {
            if (directorySegment.StartsWith(".", StringComparison.Ordinal))
            {
                return true;
            }

            if (ExcludedDirectorySegments.Contains(directorySegment))
            {
                return true;
            }
        }

        return false;
    }
}
