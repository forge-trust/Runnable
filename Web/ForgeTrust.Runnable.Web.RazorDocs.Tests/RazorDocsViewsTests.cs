namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public class RazorDocsViewsTests
{
    [Fact]
    public void Layout_ShouldContain_SearchShellMarkers()
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        var layoutPath = Path.Combine(
            repoRoot,
            "Web",
            "ForgeTrust.Runnable.Web.RazorDocs",
            "Views",
            "Shared",
            "_Layout.cshtml");

        var layout = File.ReadAllText(layoutPath);
        Assert.Contains("id=\"docs-search-input\"", layout);
        Assert.Contains("id=\"docs-search-results\"", layout);
        Assert.Contains("href=\"~/docs/search.css\"", layout);
        Assert.Contains("href=\"/docs/search-index.json\"", layout);
        Assert.Contains("src=\"~/docs/search-client.js\"", layout);
    }

    private static string FindRepoRoot(string startPath)
    {
        var current = new DirectoryInfo(startPath);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ForgeTrust.Runnable.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
