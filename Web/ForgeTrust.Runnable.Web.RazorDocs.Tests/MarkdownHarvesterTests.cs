using FakeItEasy;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public class MarkdownHarvesterTests : IDisposable
{
    private readonly ILogger<MarkdownHarvester> _loggerFake;
    private readonly MarkdownHarvester _harvester;
    private readonly string _testRoot;

    public MarkdownHarvesterTests()
    {
        _loggerFake = A.Fake<ILogger<MarkdownHarvester>>();
        _harvester = new MarkdownHarvester(_loggerFake);
        _testRoot = Path.Combine(Path.GetTempPath(), "RazorDocsTests_MD", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testRoot);
    }

    [Fact]
    public async Task HarvestAsync_ShouldIgnoreExcludedDirectories()
    {
        // Arrange
        var binDir = Path.Combine(_testRoot, "bin");
        Directory.CreateDirectory(binDir);
        await File.WriteAllTextAsync(Path.Combine(binDir, "Ignored.md"), "# Ignored");

        var srcDir = Path.Combine(_testRoot, "src");
        Directory.CreateDirectory(srcDir);
        await File.WriteAllTextAsync(Path.Combine(srcDir, "Included.md"), "# Included");

        // Act
        var results = (await _harvester.HarvestAsync(_testRoot)).ToList();

        // Assert
        Assert.Single(results);
        Assert.Contains(results, n => n.Title == "Included");
        Assert.DoesNotContain(results, n => n.Title == "Ignored");
    }

    [Fact]
    public async Task HarvestAsync_ShouldParseMarkdownToHtml()
    {
        // Arrange
        var content = "# Hello World\nThis is a *test*.";
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "Test.md"), content);

        // Act
        var results = await _harvester.HarvestAsync(_testRoot);
        var doc = results.Single();

        // Assert
        Assert.Equal("Test", doc.Title);
        Assert.Contains("<h1 id=\"hello-world\">Hello World</h1>", doc.Content);
        Assert.Contains("<em>test</em>", doc.Content);
    }

    [Fact]
    public async Task HarvestAsync_ShouldHandleFileReadErrorsGracefully()
    {
        // Arrange
        var lockedFile = Path.Combine(_testRoot, "Locked.md");
        await File.WriteAllTextAsync(lockedFile, "secret");

        using var open = File.Open(lockedFile, FileMode.Open, FileAccess.Read, FileShare.None);
        // Act - File is locked
        var results = await _harvester.HarvestAsync(_testRoot);

        // Assert
        Assert.Empty(results);
        // Verify logger was called
        A.CallTo(_loggerFake).Where(call => call.Method.Name == "Log").MustHaveHappened();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            try
            {
                Directory.Delete(_testRoot, true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }
}
