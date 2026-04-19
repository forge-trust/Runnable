using ForgeTrust.Runnable.Web.RazorDocs.Services;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public sealed class RepositoryReadmePolicyTests
{
    private static readonly HashSet<string> AuthoredReadmeExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
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
            .Where(path => !HarvestPathExclusions.ShouldExcludeFilePath(path, AuthoredReadmeExcludedDirectories))
            .ToArray();
    }
}
