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
    public async Task HarvestAsync_ShouldParseFrontMatterMetadata_AndRemoveItFromRenderedHtml()
    {
        var content = """
            ---
            title: Quickstart
            summary: Build your first app.
            page_type: guide
            audience: implementer
            component: RazorWire
            aliases:
              - getting started
              - first app
            redirect_aliases:
              - guides/getting-started
              - intro/quickstart
            keywords: [turbo, streams]
            nav_group: Start Here
            order: 10
            sequence_key: getting-started
            hide_from_public_nav: true
            hide_from_search: false
            related_pages:
              - Security & Anti-Forgery
            canonical_slug: start/quickstart
            breadcrumbs:
              - Start Here
              - Quickstart
            ---
            # Hello World

            This is a guide.
            """;
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "Guide.md"), content);

        var results = (await _harvester.HarvestAsync(_testRoot)).ToList();
        var doc = Assert.Single(results);

        Assert.Equal("Quickstart", doc.Title);
        Assert.DoesNotContain("page_type", doc.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Build your first app.", doc.Metadata?.Summary);
        Assert.False(doc.Metadata?.SummaryIsDerived);
        Assert.Equal("guide", doc.Metadata?.PageType);
        Assert.Equal("implementer", doc.Metadata?.Audience);
        Assert.Equal("RazorWire", doc.Metadata?.Component);
        Assert.Equal(["getting started", "first app"], doc.Metadata?.Aliases);
        Assert.Equal(["guides/getting-started", "intro/quickstart"], doc.Metadata?.RedirectAliases);
        Assert.Equal(["turbo", "streams"], doc.Metadata?.Keywords);
        Assert.Equal("Start Here", doc.Metadata?.NavGroup);
        Assert.Equal(10, doc.Metadata?.Order);
        Assert.Equal("getting-started", doc.Metadata?.SequenceKey);
        Assert.True(doc.Metadata?.HideFromPublicNav);
        Assert.False(doc.Metadata?.HideFromSearch);
        Assert.Equal(["Security & Anti-Forgery"], doc.Metadata?.RelatedPages);
        Assert.Equal("start/quickstart", doc.Metadata?.CanonicalSlug);
        Assert.Equal(["Start Here", "Quickstart"], doc.Metadata?.Breadcrumbs);
    }

    [Fact]
    public async Task HarvestAsync_ShouldCaptureOutlineFromMarkdownAst()
    {
        var content = """
            # Quickstart

            Intro paragraph.

            ## Install

            ### Verify Setup

            #### Deep Detail
            """;
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "Guide.md"), content);

        var doc = Assert.Single(await _harvester.HarvestAsync(_testRoot));

        Assert.NotNull(doc.Outline);
        Assert.Collection(
            doc.Outline!,
            first =>
            {
                Assert.Equal("Install", first.Title);
                Assert.Equal("install", first.Id);
                Assert.Equal(2, first.Level);
            },
            second =>
            {
                Assert.Equal("Verify Setup", second.Title);
                Assert.Equal("verify-setup", second.Id);
                Assert.Equal(3, second.Level);
            });
    }

    [Fact]
    public async Task HarvestAsync_ShouldReadRootReadmeMetadataFromPairedSidecar()
    {
        var guidesDir = Path.Combine(_testRoot, "guides");
        Directory.CreateDirectory(guidesDir);
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "README.md"), "# Runnable");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "README.md.yml"),
            """
            title: Runnable
            summary: Start with the proof paths that matter most.
            featured_pages:
              - question: Where do I start?
                path: guides/intro.md
                supporting_copy: Follow the intro guide first.
                order: 10
            """);
        await File.WriteAllTextAsync(Path.Combine(guidesDir, "intro.md"), "# Intro");

        var results = (await _harvester.HarvestAsync(_testRoot)).ToList();
        var doc = results.Single(n => n.Path == "README.md");

        Assert.Equal("Runnable", doc.Title);
        Assert.Equal("Start with the proof paths that matter most.", doc.Metadata?.Summary);
        Assert.False(doc.Metadata?.SummaryIsDerived);
        var featuredPage = Assert.Single(doc.Metadata?.FeaturedPages!);
        Assert.Equal("Where do I start?", featuredPage.Question);
        Assert.Equal("guides/intro.md", featuredPage.Path);
        Assert.Equal("Follow the intro guide first.", featuredPage.SupportingCopy);
        Assert.Equal(10, featuredPage.Order);
    }

    [Fact]
    public async Task HarvestAsync_ShouldReadSingleMdYamlSidecar()
    {
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "Guide.md"), "# Guide");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "Guide.md.yaml"), "title: YAML Sidecar Title");

        var results = (await _harvester.HarvestAsync(_testRoot)).ToList();
        var doc = Assert.Single(results);

        Assert.Equal("YAML Sidecar Title", doc.Title);
        Assert.Equal("YAML Sidecar Title", doc.Metadata?.Title);
        A.CallTo(_loggerFake)
            .Where(
                call => call.Method.Name == nameof(ILogger.Log)
                        && call.GetArgument<LogLevel>(0) == LogLevel.Warning)
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task HarvestAsync_ShouldPreferInlineFrontMatterOverSidecarMetadata()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_testRoot, "Guide.md"),
            """
            ---
            title: Inline Quickstart
            summary: Inline summary wins.
            ---
            # Hello

            Inline body.
            """);
        await File.WriteAllTextAsync(
            Path.Combine(_testRoot, "Guide.md.yml"),
            """
            title: Sidecar Quickstart
            summary: Sidecar summary should lose.
            keywords:
              - paired
              - fallback
            """);

        var results = (await _harvester.HarvestAsync(_testRoot)).ToList();
        var doc = Assert.Single(results);

        Assert.Equal("Inline Quickstart", doc.Title);
        Assert.Equal("Inline summary wins.", doc.Metadata?.Summary);
        Assert.False(doc.Metadata?.SummaryIsDerived);
        Assert.Equal(["paired", "fallback"], doc.Metadata?.Keywords);
    }

    [Fact]
    public async Task HarvestAsync_ShouldTreatExplicitEmptyInlineListsAsAuthoritativeOverSidecarMetadata()
    {
        var guidesDir = Path.Combine(_testRoot, "guides");
        Directory.CreateDirectory(guidesDir);
        await File.WriteAllTextAsync(
            Path.Combine(_testRoot, "README.md"),
            """
            ---
            featured_pages: []
            ---
            # Runnable
            """);
        await File.WriteAllTextAsync(
            Path.Combine(_testRoot, "README.md.yml"),
            """
            featured_pages:
              - path: guides/intro.md
            """);
        await File.WriteAllTextAsync(Path.Combine(guidesDir, "intro.md"), "# Intro");

        var results = (await _harvester.HarvestAsync(_testRoot)).ToList();
        var doc = results.Single(n => n.Path == "README.md");

        Assert.Empty(doc.Metadata?.FeaturedPages!);
    }

    [Fact]
    public async Task HarvestAsync_ShouldIgnoreMetadataSidecars_WhenBothYamlExtensionsExist()
    {
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "Guide.md"), "# Guide");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "Guide.md.yml"), "title: First");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "Guide.md.yaml"), "title: Second");

        var results = (await _harvester.HarvestAsync(_testRoot)).ToList();
        var doc = Assert.Single(results);

        Assert.Equal("Guide", doc.Title);
        Assert.Equal("Guide", doc.Metadata?.Title);
        AssertWarningLogged("both");
    }

    [Fact]
    public async Task HarvestAsync_ShouldIgnoreInvalidMetadataSidecar_AndLogWarning()
    {
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "Guide.md"), "# Guide");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "Guide.md.yml"), "title: [");

        var results = (await _harvester.HarvestAsync(_testRoot)).ToList();
        var doc = Assert.Single(results);

        Assert.Equal("Guide", doc.Title);
        Assert.Equal("Guide", doc.Metadata?.Title);
        AssertWarningLogged("could not be parsed");
    }

    [Fact]
    public async Task HarvestAsync_ShouldIgnoreUnreadableMetadataSidecar_AndLogWarning()
    {
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "Guide.md"), "# Guide");
        await File.WriteAllTextAsync(Path.Combine(_testRoot, "Guide.md.yml"), "title: Hidden");
        var harvester = new MarkdownHarvester(
            _loggerFake,
            (path, cancellationToken) => path.EndsWith(".md.yml", StringComparison.OrdinalIgnoreCase)
                ? Task.FromException<string>(new IOException("boom"))
                : File.ReadAllTextAsync(path, cancellationToken));

        var results = (await harvester.HarvestAsync(_testRoot)).ToList();
        var doc = Assert.Single(results);

        Assert.Equal("Guide", doc.Title);
        Assert.Equal("Guide", doc.Metadata?.Title);
        AssertWarningLogged("could not be read");
    }

    [Fact]
    public async Task ReadMetadataSidecarAsync_ShouldThrow_WhenMarkdownPathIsBlank()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _harvester.ReadMetadataSidecarAsync(" ", "Guide.md", CancellationToken.None));
    }

    [Fact]
    public async Task ReadMetadataSidecarAsync_ShouldThrow_WhenRelativeMarkdownPathIsBlank()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _harvester.ReadMetadataSidecarAsync(Path.Combine(_testRoot, "Guide.md"), " ", CancellationToken.None));
    }

    [Fact]
    public async Task ReadMetadataSidecarAsync_ShouldPropagateOperationCanceled_WhenSidecarReadIsCanceled()
    {
        var markdownPath = Path.Combine(_testRoot, "Guide.md");
        await File.WriteAllTextAsync(markdownPath, "# Guide");
        await File.WriteAllTextAsync(markdownPath + ".yml", "title: Hidden");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var harvester = new MarkdownHarvester(
            _loggerFake,
            (path, cancellationToken) => path.EndsWith(".md.yml", StringComparison.OrdinalIgnoreCase)
                ? Task.FromCanceled<string>(cts.Token)
                : File.ReadAllTextAsync(path, cancellationToken));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => harvester.ReadMetadataSidecarAsync(markdownPath, "Guide.md", cts.Token));
        A.CallTo(_loggerFake).Where(call => call.Method.Name == "Log").MustNotHaveHappened();
    }

    [Fact]
    public async Task HarvestAsync_ShouldDeriveSummary_WhenFrontMatterSummaryIsMissing()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_testRoot, "Guide.md"),
            """
            # Heading

            This is the first paragraph.

            ## Next

            More content.
            """);

        var results = (await _harvester.HarvestAsync(_testRoot)).ToList();
        var doc = Assert.Single(results);

        Assert.Equal("This is the first paragraph.", doc.Metadata?.Summary);
        Assert.True(doc.Metadata?.SummaryIsDerived);
        Assert.Equal("guide", doc.Metadata?.PageType);
    }

    [Fact]
    public async Task HarvestAsync_ShouldHideInternalMarkdownFromSearchByDefault()
    {
        var testsDir = Path.Combine(_testRoot, "docs", "ForgeTrust.Runnable.Web.Tests");
        Directory.CreateDirectory(testsDir);
        await File.WriteAllTextAsync(Path.Combine(testsDir, "README.md"), "# Internal Guide");

        var results = (await _harvester.HarvestAsync(_testRoot)).ToList();
        var doc = Assert.Single(results);

        Assert.True(doc.Metadata?.HideFromPublicNav);
        Assert.True(doc.Metadata?.HideFromSearch);
        Assert.Equal("internals", doc.Metadata?.PageType);
    }

    [Fact]
    public void ExtractSummary_ShouldReturnNull_ForWhitespaceInput()
    {
        var summary = MarkdownHarvester.ExtractSummary("   \n  ");

        Assert.Null(summary);
    }

    [Fact]
    public void ExtractSummary_ShouldSkipCodeFences_BeforeCollectingSummary()
    {
        var summary = MarkdownHarvester.ExtractSummary(
            """
            ```csharp
            var ignored = true;
            ```

            This is the summary paragraph.
            """);

        Assert.Equal("This is the summary paragraph.", summary);
    }

    [Fact]
    public void ExtractSummary_ShouldStopWhenListAppearsAfterSummaryText()
    {
        var summary = MarkdownHarvester.ExtractSummary(
            """
            This is the summary paragraph.
            - Follow-up detail
            """);

        Assert.Equal("This is the summary paragraph.", summary);
    }

    [Fact]
    public void ExtractSummary_ShouldIgnoreNumberedListsAtStart()
    {
        var summary = MarkdownHarvester.ExtractSummary(
            """
            1. Install the package
            2. Configure the service

            This is the first paragraph.
            """);

        Assert.Equal("This is the first paragraph.", summary);
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

    private void AssertWarningLogged(string expectedMessageFragment)
    {
        A.CallTo(_loggerFake)
            .Where(
                call => call.Method.Name == nameof(ILogger.Log)
                        && call.GetArgument<LogLevel>(0) == LogLevel.Warning
                        && LoggedMessageContains(call, expectedMessageFragment))
            .MustHaveHappened();
    }

    private static bool LoggedMessageContains(FakeItEasy.Core.IFakeObjectCall call, string expectedMessageFragment)
    {
        var message = call.GetArgument<object>(2)?.ToString();
        return message?.Contains(expectedMessageFragment, StringComparison.OrdinalIgnoreCase) == true;
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
