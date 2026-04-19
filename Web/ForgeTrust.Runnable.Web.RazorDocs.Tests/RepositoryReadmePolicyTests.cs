using System.Diagnostics;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public sealed class RepositoryReadmePolicyTests
{
    [Fact]
    public void TrackedReadmes_ShouldNotStartWithYamlFrontMatter()
    {
        var repoRoot = TestPathUtils.FindRepoRoot(AppContext.BaseDirectory);
        var violatingReadmes = GetTrackedReadmePaths(repoRoot)
            .Where(path => path.EndsWith("README.md", StringComparison.OrdinalIgnoreCase))
            .Where(
                path => File.ReadAllText(Path.Combine(repoRoot, path))
                    .Replace("\r\n", "\n", StringComparison.Ordinal)
                    .StartsWith("---\n", StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            violatingReadmes.Length == 0,
            $"Tracked README.md files must stay portable and avoid inline YAML front matter. Violations: {string.Join(", ", violatingReadmes)}");
    }

    private static IReadOnlyList<string> GetTrackedReadmePaths(string repoRoot)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("ls-files");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start git ls-files.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git ls-files failed while collecting tracked README paths: {standardError}");
        }

        return standardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }
}
