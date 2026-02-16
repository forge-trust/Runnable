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
    public async Task HarvestAsync_ShouldIgnoreCommonAgentDirectories()
    {
        // Arrange
        var agentDir = Path.Combine(_testRoot, ".claude");
        Directory.CreateDirectory(agentDir);
        await File.WriteAllTextAsync(Path.Combine(agentDir, "Ignored.md"), "# Agent");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "Included.md"), "# Included");

        // Act
        var results = (await _harvester.HarvestAsync(_testRoot)).ToList();

        // Assert
        Assert.Single(results);
        Assert.Contains(results, n => n.Title == "Included");
        Assert.DoesNotContain(results, n => n.Title == "Ignored");
    }

    [Fact]
    public async Task HarvestAsync_ShouldIgnoreDotPrefixedDirectories_IncludingGithub()
    {
        // Arrange
        var hiddenDir = Path.Combine(_testRoot, ".github");
        Directory.CreateDirectory(hiddenDir);
        await File.WriteAllTextAsync(Path.Combine(hiddenDir, "Ignored.md"), "# Ignored");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "Included.md"), "# Included");

        // Act
        var results = (await _harvester.HarvestAsync(_testRoot)).ToList();

        // Assert
        Assert.Single(results);
        Assert.Contains(results, n => n.Title == "Included");
        Assert.DoesNotContain(results, n => n.Title == "Ignored");
    }

    [Fact]
    public async Task HarvestAsync_ShouldIncludeDotPrefixedFiles()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testRoot, ".hidden.md"), "# Hidden");

        // Act
        var results = (await _harvester.HarvestAsync(_testRoot)).ToList();

        // Assert
        Assert.Single(results);
        Assert.Contains(results, n => n.Title == ".hidden");
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
    public void Constructor_ShouldThrow_WhenLoggerIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new MarkdownHarvester(null!, (_, _) => Task.FromResult(string.Empty)));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenReadDelegateIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new MarkdownHarvester(_loggerFake, null!));
    }

    [Fact]
    public async Task HarvestAsync_ShouldLogAndSkip_WhenReadDelegateThrows()
    {
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "Good.md"), "# Good");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "Broken.md"), "# Broken");
        var harvester = new MarkdownHarvester(
            _loggerFake,
            (path, cancellationToken) => path.EndsWith("Broken.md", StringComparison.Ordinal)
                ? Task.FromException<string>(new IOException("boom"))
                : File.ReadAllTextAsync(path, cancellationToken));

        var results = (await harvester.HarvestAsync(_testRoot)).ToList();

        Assert.Single(results);
        Assert.Equal("Good", results[0].Title);
        A.CallTo(_loggerFake).Where(call => call.Method.Name == "Log").MustHaveHappened();
    }

    [Fact]
    public async Task HarvestAsync_ShouldUseParentDirNameForREADME()
    {
        // Arrange
        var subDir = Path.Combine(_testRoot, "Components");
        Directory.CreateDirectory(subDir);
        await File.WriteAllTextAsync(Path.Combine(subDir, "README.md"), "# Components Guide");

        // Act
        var results = (await _harvester.HarvestAsync(_testRoot)).ToList();

        // Assert
        var doc = results.Single(n => n.Path.Contains("README.md"));
        Assert.Equal("Components", doc.Title);
    }

    [Fact]
    public async Task HarvestAsync_ShouldReturnHomeForRootREADME()
    {
        // Arrange
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "README.md"), "# Project Home");

        // Act
        var results = (await _harvester.HarvestAsync(_testRoot)).ToList();

        // Assert
        var doc = results.Single(n => n.Title == "Home");
        Assert.Equal("Home", doc.Title);
    }

    [Fact]
    public async Task HarvestAsync_ShouldRespectCancellation()
    {
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "Test.md"), "# Test");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _harvester.HarvestAsync(_testRoot, cts.Token));
    }

    [Fact]
    public async Task HarvestAsync_ShouldPropagateOperationCanceled_WhenReadDelegateThrows()
    {
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "Cancel.md"), "# Cancel");
        var harvester = new MarkdownHarvester(
            _loggerFake,
            (_, cancellationToken) => throw new OperationCanceledException(cancellationToken));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harvester.HarvestAsync(_testRoot));
        A.CallTo(_loggerFake).Where(call => call.Method.Name == "Log").MustNotHaveHappened();
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
