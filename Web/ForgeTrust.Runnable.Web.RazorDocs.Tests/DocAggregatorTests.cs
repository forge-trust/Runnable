using AngleSharp;
using FakeItEasy;
using ForgeTrust.Runnable.Caching;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using Ganss.Xss;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public class DocAggregatorTests : IDisposable
{
    private readonly IDocHarvester _harvesterFake;
    private readonly RazorDocsOptions _options;
    private readonly IWebHostEnvironment _envFake;
    private readonly ILogger<DocAggregator> _loggerFake;
    private readonly IRazorDocsHtmlSanitizer _sanitizerFake;
    private readonly IMemoryCache _cache;
    private readonly IMemo _memo;
    private readonly DocAggregator _aggregator;

    public DocAggregatorTests()
    {
        _harvesterFake = A.Fake<IDocHarvester>();
        _options = new RazorDocsOptions();
        _envFake = A.Fake<IWebHostEnvironment>();
        _loggerFake = A.Fake<ILogger<DocAggregator>>();
        _sanitizerFake = A.Fake<IRazorDocsHtmlSanitizer>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _memo = new Memo(_cache);

        A.CallTo(() => _envFake.ContentRootPath).Returns(Path.GetTempPath());

        // Default: just return input for sanitization in most tests
        A.CallTo(() => _sanitizerFake.Sanitize(A<string>._))
            .ReturnsLazily((string input) => input);

        _aggregator = new DocAggregator(
            new[] { _harvesterFake },
            _options,
            _envFake,
            _memo,
            _sanitizerFake,
            _loggerFake
        );
    }

    [Fact]
    public async Task GetDocsAsync_ShouldReturnCachedResults_WhenCacheExists()
    {
        // Arrange
        var cachedDocs = new List<DocNode> { new("Cached", "path", "content") };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(cachedDocs);

        _ = await _aggregator.GetDocsAsync();

        // Act
        var result = await _aggregator.GetDocsAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Cached", result.First().Title);
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetDocsAsync_ShouldHarvestAndCache_WhenCacheMiss()
    {
        // Arrange
        var harvestedDocs = new List<DocNode> { new DocNode("Fresh", "path", "content") };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(harvestedDocs);

        // Act
        var firstResult = await _aggregator.GetDocsAsync();
        var secondResult = await _aggregator.GetDocsAsync();

        // Assert
        Assert.Single(firstResult);
        Assert.Single(secondResult);
        Assert.Equal("Fresh", firstResult.First().Title);
        Assert.Equal("Fresh", secondResult.First().Title);
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetDocsAsync_ShouldSanitizeContent_WhenHarvested()
    {
        // Arrange
        var unsafeHtml = "<script>alert('xss')</script><p>Safe</p>";
        var safeHtml = "<p>Safe</p>";
        var harvestedDocs = new List<DocNode> { new DocNode("Title", "path", unsafeHtml) };

        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(harvestedDocs);
        A.CallTo(() => _sanitizerFake.Sanitize(unsafeHtml))
            .Returns(safeHtml);

        // Act
        var result = await _aggregator.GetDocsAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal(safeHtml, result.First().Content);
    }

    [Fact]
    public async Task GetDocsAsync_ShouldPreserveMetadata_WhenSanitizingHarvestedNodes()
    {
        var harvestedDocs = new List<DocNode>
        {
            new(
                "Title",
                "path",
                "<p>content</p>",
                Metadata: new DocMetadata
                {
                    Summary = "Summary",
                    PageType = "guide",
                    HideFromSearch = true
                })
        };

        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(harvestedDocs);

        var result = Assert.Single((await _aggregator.GetDocsAsync()).ToList());

        Assert.Equal("Summary", result.Metadata?.Summary);
        Assert.Equal("guide", result.Metadata?.PageType);
        Assert.True(result.Metadata?.HideFromSearch);
    }

    [Fact]
    public async Task GetDocsAsync_ShouldHandleDuplicatePaths_ByKeepingFirst()
    {
        // Arrange
        var harvestedDocs = new List<DocNode>
        {
            new DocNode("First", "duplicate-path", "content1"),
            new DocNode("Second", "duplicate-path", "content2")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(harvestedDocs);

        // Act
        var result = await _aggregator.GetDocsAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("First", result.First().Title);
    }

    [Fact]
    public async Task GetDocsAsync_ShouldHandleHarvesterExceptions_ByLoggingAndSkipping()
    {
        // Arrange
        var failingHarvester = A.Fake<IDocHarvester>();
        A.CallTo(() => failingHarvester.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Throws(new Exception("Harvester boom"));

        var workingHarvester = A.Fake<IDocHarvester>();
        A.CallTo(() => workingHarvester.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Returns(new List<DocNode> { new DocNode("Success", "path", "content") });

        var aggregator = new DocAggregator(
            new[] { failingHarvester, workingHarvester },
            _options,
            _envFake,
            _memo,
            _sanitizerFake,
            _loggerFake
        );

        // Act
        var result = await aggregator.GetDocsAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Success", result.First().Title);
    }

    [Fact]
    public async Task GetDocByPathAsync_WhenDocNotFound_ReturnsNull()
    {
        // Arrange
        var aggregator = new DocAggregator(
            Enumerable.Empty<IDocHarvester>(),
            new RazorDocsOptions(),
            _envFake,
            _memo,
            _sanitizerFake,
            _loggerFake);

        // Act
        var result = await aggregator.GetDocByPathAsync("non-existent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetDocByPathAsync_WhenPathIsNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _aggregator.GetDocByPathAsync(null!));
    }

    [Fact]
    public async Task GetDocsAsync_ShouldExposeCanonicalHtmlPaths_AndResolveByCanonicalPath()
    {
        // Arrange
        var harvestedDocs = new List<DocNode>
        {
            new("Guide", "docs/readme.md", "content"),
            new("Method", "docs/service.cs#MethodId", "content", "docs/service.cs")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(harvestedDocs);

        // Act
        var docs = await _aggregator.GetDocsAsync();
        var byCanonical = await _aggregator.GetDocByPathAsync("docs/readme.md.html");
        var byCanonicalWithoutAnchor = await _aggregator.GetDocByPathAsync("docs/service.cs.html");

        // Assert
        Assert.Contains(docs, d => d.Path == "docs/readme.md" && d.CanonicalPath == "docs/readme.md.html");
        Assert.Contains(
            docs,
            d => d.Path == "docs/service.cs#MethodId" && d.CanonicalPath == "docs/service.cs.html#MethodId");
        Assert.NotNull(byCanonical);
        Assert.Equal("Guide", byCanonical!.Title);
        Assert.NotNull(byCanonicalWithoutAnchor);
        Assert.Equal("Method", byCanonicalWithoutAnchor!.Title);
    }

    [Fact]
    public async Task GetDocsAsync_ShouldUseConfiguredRepositoryRoot_WhenProvided()
    {
        // Arrange
        var configuredRoot = Path.Combine(Path.GetTempPath(), "repo-root");
        var options = new RazorDocsOptions
        {
            Source = new RazorDocsSourceOptions { RepositoryRoot = $"  {configuredRoot}  " }
        };
        string? capturedRoot = null;
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Invokes((string root, CancellationToken _) => capturedRoot = root)
            .Returns(Array.Empty<DocNode>());
        var aggregator = new DocAggregator(
            new[] { _harvesterFake },
            options,
            _envFake,
            _memo,
            _sanitizerFake,
            _loggerFake);

        // Act
        _ = await aggregator.GetDocsAsync();

        // Assert
        Assert.Equal(configuredRoot, capturedRoot);
    }

    [Fact]
    public async Task GetDocsAsync_ShouldMapEmptyAndWhitespacePaths_ToIndexCanonicalPath()
    {
        // Arrange
        var harvestedDocs = new List<DocNode>
        {
            new("Home", " ", "content"),
            new("AnchoredHome", "#overview", "content")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(harvestedDocs);

        // Act
        var docs = await _aggregator.GetDocsAsync();

        // Assert
        Assert.Contains(docs, d => d.Path == " " && d.CanonicalPath == "index.html");
        Assert.Contains(docs, d => d.Path == "#overview" && d.CanonicalPath == "index.html#overview");
    }

    [Fact]
    public async Task GetDocByPathAsync_ShouldMatchCanonicalPath_WithFragmentInLookup()
    {
        // Arrange
        var harvestedDocs = new List<DocNode> { new("Method", "docs/service.cs#MethodId", "content") };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(harvestedDocs);

        // Act
        var result = await _aggregator.GetDocByPathAsync("docs/service.cs.html#MethodId");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Method", result!.Title);
    }

    [Fact]
    public async Task GetDocByPathAsync_ShouldPreferNamespacePage_WhenCanonicalLookupHasNoFragment()
    {
        // Arrange
        var harvestedDocs = new List<DocNode>
        {
            new("FooType", "Namespaces/Foo#Foo-Type", string.Empty),
            new("Foo", "Namespaces/Foo", "<p>Namespace page</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(harvestedDocs);

        // Act
        var result = await _aggregator.GetDocByPathAsync("Namespaces/Foo.html");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Namespaces/Foo", result!.Path);
        Assert.Equal("Foo", result.Title);
    }

    [Fact]
    public async Task GetDocByPathAsync_ShouldFallbackWhenLookupFragmentMissing_AndDocHasNoFragment()
    {
        // Arrange
        var harvestedDocs = new List<DocNode>
        {
            new("Service", "docs/service.cs", "<p>Service</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(harvestedDocs);

        // Act
        var result = await _aggregator.GetDocByPathAsync("docs/service.cs.html#MissingFragment");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Service", result!.Title);
    }

    [Fact]
    public async Task GetDocsAsync_ShouldSkipCanceledHarvester_AndContinue()
    {
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Throws(new OperationCanceledException());

        var docs = await _aggregator.GetDocsAsync();

        Assert.Empty(docs);
    }

    [Fact]
    public async Task GetDocsAsync_ShouldUseInstanceScopedMemoKeys_WhenMemoIsShared()
    {
        using var sharedCache = new MemoryCache(new MemoryCacheOptions());
        using var sharedMemo = new Memo(sharedCache);

        var sharedEnv = A.Fake<IWebHostEnvironment>();
        A.CallTo(() => sharedEnv.ContentRootPath).Returns(Path.GetTempPath());
        var sharedSanitizer = A.Fake<IRazorDocsHtmlSanitizer>();
        A.CallTo(() => sharedSanitizer.Sanitize(A<string>._))
            .ReturnsLazily((string input) => input);
        var sharedLogger = A.Fake<ILogger<DocAggregator>>();

        var harvesterA = A.Fake<IDocHarvester>();
        var harvesterB = A.Fake<IDocHarvester>();
        A.CallTo(() => harvesterA.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Returns(new[] { new DocNode("DocA", "a", "content") });
        A.CallTo(() => harvesterB.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Returns(new[] { new DocNode("DocB", "b", "content") });

        var sharedOptions = new RazorDocsOptions
        {
            Source = new RazorDocsSourceOptions { RepositoryRoot = Path.GetTempPath() }
        };

        var aggregatorA = new DocAggregator(
            new[] { harvesterA },
            sharedOptions,
            sharedEnv,
            sharedMemo,
            sharedSanitizer,
            sharedLogger);
        var aggregatorB = new DocAggregator(
            new[] { harvesterB },
            sharedOptions,
            sharedEnv,
            sharedMemo,
            sharedSanitizer,
            sharedLogger);

        var docsA = await aggregatorA.GetDocsAsync();
        var docsB = await aggregatorB.GetDocsAsync();

        Assert.Single(docsA);
        Assert.Single(docsB);
        Assert.Equal("DocA", docsA[0].Title);
        Assert.Equal("DocB", docsB[0].Title);
        A.CallTo(() => harvesterA.HarvestAsync(A<string>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => harvesterB.HarvestAsync(A<string>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task InvalidateCache_ShouldNotResurfaceOlderSnapshots_AfterMultipleRefreshes()
    {
        var harvestCount = 0;
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                harvestCount++;
                return new[] { new DocNode($"Doc{harvestCount}", $"path-{harvestCount}", "content") };
            });

        var first = (await _aggregator.GetDocsAsync()).Single();
        _aggregator.InvalidateCache();
        var second = (await _aggregator.GetDocsAsync()).Single();
        _aggregator.InvalidateCache();
        var third = (await _aggregator.GetDocsAsync()).Single();

        Assert.Equal("Doc1", first.Title);
        Assert.Equal("Doc2", second.Title);
        Assert.Equal("Doc3", third.Title);
        Assert.Equal(3, harvestCount);
    }

    [Fact]
    public async Task GetDocsAsync_ShouldPassTimeoutScopedToken_ToHarvester()
    {
        CancellationToken? observedToken = null;
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Invokes((string _, CancellationToken ct) => observedToken = ct)
            .Returns(Array.Empty<DocNode>());

        _ = await _aggregator.GetDocsAsync();

        Assert.NotNull(observedToken);
        Assert.True(observedToken!.Value.CanBeCanceled);
        Assert.False(observedToken.Value.IsCancellationRequested);
    }

    [Fact]
    public async Task GetDocsAsync_ShouldCancelCallerWait_WithoutPoisoningSharedSnapshot()
    {
        // Arrange
        var harvestedDocs = new List<DocNode> { new("Recovered", "path", "content") };
        var releaseHarvester = new TaskCompletionSource<IReadOnlyList<DocNode>>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken? observedToken = null;
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Invokes((string _, CancellationToken ct) => observedToken = ct)
            .ReturnsLazily(() => releaseHarvester.Task);

        using var cts = new CancellationTokenSource();
        var canceledCall = _aggregator.GetDocsAsync(cts.Token);

        // Act
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => _ = (await canceledCall).ToList());
        releaseHarvester.SetResult(harvestedDocs);
        var recovered = await _aggregator.GetDocsAsync();

        // Assert
        Assert.NotNull(observedToken);
        Assert.True(observedToken!.Value.CanBeCanceled);
        Assert.False(observedToken.Value.IsCancellationRequested);
        Assert.Single(recovered);
        Assert.Equal("Recovered", recovered[0].Title);
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GetDocsAsync_ShouldHandleRootFileCanonicalization()
    {
        var harvestedDocs = new List<DocNode>
        {
            new("RootFile", "readme.md", "content")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(harvestedDocs);

        var docs = await _aggregator.GetDocsAsync();

        Assert.Contains(docs, d => d.Title == "RootFile" && d.CanonicalPath == "readme.md.html");
    }

    [Fact]
    public async Task BuildCanonicalPath_ShouldPreserve_DotsToAvoidCollisions()
    {
        var harvestedDocs = new List<DocNode>
        {
            new("Dotdotted", "docs/readme.md", "content"),
            new("Underscored", "docs/readme_md.md", "content"),
            new("ApiV2", "docs/api.v2.md", "content")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(harvestedDocs);

        var docs = await _aggregator.GetDocsAsync();

        Assert.Contains(docs, d => d.Path == "docs/readme.md" && d.CanonicalPath == "docs/readme.md.html");
        Assert.Contains(docs, d => d.Path == "docs/readme_md.md" && d.CanonicalPath == "docs/readme_md.md.html");
        Assert.Contains(docs, d => d.Path == "docs/api.v2.md" && d.CanonicalPath == "docs/api.v2.md.html");
        Assert.NotEqual("docs/readme.md.html", "docs/readme_md.md.html");
    }

    [Fact]
    public async Task Constructor_ShouldPreferConfiguredRoot_OverEnvironmentFallback()
    {
        var configuredRoot = Path.Combine(Path.GetTempPath(), "configured-root");
        var localOptions = new RazorDocsOptions
        {
            Source = new RazorDocsSourceOptions { RepositoryRoot = configuredRoot }
        };
        var localEnv = A.Fake<IWebHostEnvironment>();
        A.CallTo(() => localEnv.ContentRootPath).Returns("/definitely/not/used");
        string? capturedRoot = null;
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Invokes((string root, CancellationToken _) => capturedRoot = root)
            .Returns(Array.Empty<DocNode>());
        var aggregator = new DocAggregator(
            new[] { _harvesterFake },
            localOptions,
            localEnv,
            _memo,
            _sanitizerFake,
            _loggerFake);

        _ = await aggregator.GetDocsAsync();

        Assert.Equal(configuredRoot, capturedRoot);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenMemoIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new DocAggregator(
                new[] { _harvesterFake },
                _options,
                _envFake,
                null!,
                _sanitizerFake,
                _loggerFake));

        Assert.Equal("memo", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenHarvestersIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new DocAggregator(
                null!,
                _options,
                _envFake,
                _memo,
                _sanitizerFake,
                _loggerFake));

        Assert.Equal("harvesters", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenEnvironmentIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new DocAggregator(
                new[] { _harvesterFake },
                _options,
                null!,
                _memo,
                _sanitizerFake,
                _loggerFake));

        Assert.Equal("environment", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSanitizerIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new DocAggregator(
                new[] { _harvesterFake },
                _options,
                _envFake,
                _memo,
                null!,
                _loggerFake));

        Assert.Equal("sanitizer", ex.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenLoggerIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new DocAggregator(
                new[] { _harvesterFake },
                _options,
                _envFake,
                _memo,
                _sanitizerFake,
                null!));

        Assert.Equal("logger", ex.ParamName);
    }

    [Fact]
    public async Task GetDocsAsync_ShouldMergeNamespaceReadmeIntoNamespaceNode_AndRemoveReadmeNode()
    {
        // Arrange
        var namespaceContent = "<section class='doc-namespace-groups'><h4>Namespaces</h4></section><section class='doc-type'>Type body</section>";
        var harvestedDocs = new List<DocNode>
        {
            new("Web", "Namespaces/ForgeTrust.Web", namespaceContent),
            new("README", "docs/ForgeTrust.Web/README.md", "<p>Namespace intro</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(harvestedDocs);

        // Act
        var docs = await _aggregator.GetDocsAsync();

        // Assert
        var namespaceDoc = docs.Single(d => d.Path == "Namespaces/ForgeTrust.Web");
        Assert.DoesNotContain(docs, d => d.Path == "docs/ForgeTrust.Web/README.md");
        Assert.Contains("doc-namespace-intro", namespaceDoc.Content);
        Assert.Contains("<p>Namespace intro</p>", namespaceDoc.Content);
        Assert.Contains("</section><section class=\"doc-namespace-intro\">", namespaceDoc.Content);
    }

    [Fact]
    public async Task GetDocsAsync_ShouldMergeNamespaceReadmeMetadataIntoNamespaceNode()
    {
        var namespaceContent = "<section class='doc-namespace-groups'><h4>Namespaces</h4></section><section class='doc-type'>Type body</section>";
        var harvestedDocs = new List<DocNode>
        {
            new(
                "Web",
                "Namespaces/ForgeTrust.Web",
                namespaceContent,
                Metadata: new DocMetadata
                {
                    Title = "Web",
                    PageType = "api-reference",
                    NavGroup = "API Reference"
                }),
            new(
                "README",
                "docs/ForgeTrust.Web/README.md",
                "<p>Namespace intro</p>",
                Metadata: new DocMetadata
                {
                    Title = "ForgeTrust Web",
                    Summary = "Namespace summary",
                    Aliases = ["web docs"],
                    HideFromSearch = true
                })
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(harvestedDocs);

        var docs = (await _aggregator.GetDocsAsync()).ToList();
        var namespaceDoc = docs.Single(d => d.Path == "Namespaces/ForgeTrust.Web");

        Assert.Equal("ForgeTrust Web", namespaceDoc.Title);
        Assert.Equal("ForgeTrust Web", namespaceDoc.Metadata?.Title);
        Assert.Equal("Namespace summary", namespaceDoc.Metadata?.Summary);
        Assert.Equal(["web docs"], namespaceDoc.Metadata?.Aliases);
        Assert.True(namespaceDoc.Metadata?.HideFromSearch);
        Assert.Equal("api-reference", namespaceDoc.Metadata?.PageType);
    }

    [Fact]
    public async Task GetDocsAsync_ShouldKeepApiClassification_WhenNamespaceReadmeMetadataOnlyProvidesDerivedDefaults()
    {
        var namespaceContent = "<section class='doc-namespace-groups'><h4>Namespaces</h4></section><section class='doc-type'>Type body</section>";
        var harvestedDocs = new List<DocNode>
        {
            new(
                "Web",
                "Namespaces/ForgeTrust.Runnable.Web",
                namespaceContent,
                Metadata: DocMetadataFactory.CreateApiReferenceMetadata("Web", "ForgeTrust.Runnable.Web")),
            new(
                "README",
                "docs/ForgeTrust.Runnable.Web/README.md",
                "<p>Namespace intro</p>",
                Metadata: DocMetadataFactory.CreateMarkdownMetadata(
                    "docs/ForgeTrust.Runnable.Web/README.md",
                    "ForgeTrust.Runnable.Web",
                    null,
                    "Namespace intro."))
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(harvestedDocs);

        var docs = (await _aggregator.GetDocsAsync()).ToList();
        var namespaceDoc = docs.Single(d => d.Path == "Namespaces/ForgeTrust.Runnable.Web");

        Assert.Equal("api-reference", namespaceDoc.Metadata?.PageType);
        Assert.Equal("developer", namespaceDoc.Metadata?.Audience);
        Assert.Equal("API Reference", namespaceDoc.Metadata?.NavGroup);
        Assert.Equal("Namespace intro.", namespaceDoc.Metadata?.Summary);
    }

    [Fact]
    public async Task GetDocsAsync_ShouldAllowExplicitNamespaceReadmeClassificationOverrides()
    {
        var namespaceContent = "<section class='doc-namespace-groups'><h4>Namespaces</h4></section><section class='doc-type'>Type body</section>";
        var harvestedDocs = new List<DocNode>
        {
            new(
                "Web",
                "Namespaces/ForgeTrust.Runnable.Web",
                namespaceContent,
                Metadata: DocMetadataFactory.CreateApiReferenceMetadata("Web", "ForgeTrust.Runnable.Web")),
            new(
                "README",
                "docs/ForgeTrust.Runnable.Web/README.md",
                "<p>Namespace intro</p>",
                Metadata: DocMetadataFactory.CreateMarkdownMetadata(
                    "docs/ForgeTrust.Runnable.Web/README.md",
                    "ForgeTrust.Runnable.Web",
                    new DocMetadata
                    {
                        PageType = "concept",
                        Audience = "implementer",
                        Component = "Docs",
                        NavGroup = "Concepts"
                    },
                    "Namespace intro."))
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(harvestedDocs);

        var docs = (await _aggregator.GetDocsAsync()).ToList();
        var namespaceDoc = docs.Single(d => d.Path == "Namespaces/ForgeTrust.Runnable.Web");

        Assert.Equal("concept", namespaceDoc.Metadata?.PageType);
        Assert.Equal("implementer", namespaceDoc.Metadata?.Audience);
        Assert.Equal("Docs", namespaceDoc.Metadata?.Component);
        Assert.Equal("Concepts", namespaceDoc.Metadata?.NavGroup);
    }

    [Fact]
    public void MergeNamespaceIntroIntoContent_ShouldHandleMalformedNamespaceSections()
    {
        // Act
        var noMarker = DocAggregator.MergeNamespaceIntroIntoContent("<section>Body</section>", "<p>Intro</p>");
        var noSectionStart = DocAggregator.MergeNamespaceIntroIntoContent("doc-namespace-groups", "<p>Intro</p>");
        var noStartTagEnd = DocAggregator.MergeNamespaceIntroIntoContent("<section class='doc-namespace-groups'", "<p>Intro</p>");
        var noEndTag = DocAggregator.MergeNamespaceIntroIntoContent("<section class='doc-namespace-groups'>open", "<p>Intro</p>");
        var malformedNestedOpen = DocAggregator.MergeNamespaceIntroIntoContent(
            "<section class='doc-namespace-groups'><section class='inner'</section></section>",
            "<p>Intro</p>");
        var unbalancedNested = DocAggregator.MergeNamespaceIntroIntoContent(
            "<section class='doc-namespace-groups'><section></section>",
            "<p>Intro</p>");

        // Assert
        Assert.Equal("<section class=\"doc-namespace-intro\"><p>Intro</p></section><section>Body</section>", noMarker);
        Assert.Equal("<section class=\"doc-namespace-intro\"><p>Intro</p></section>doc-namespace-groups", noSectionStart);
        Assert.Equal("<section class=\"doc-namespace-intro\"><p>Intro</p></section><section class='doc-namespace-groups'", noStartTagEnd);
        Assert.Equal("<section class=\"doc-namespace-intro\"><p>Intro</p></section><section class='doc-namespace-groups'>open", noEndTag);
        Assert.Equal(
            "<section class=\"doc-namespace-intro\"><p>Intro</p></section><section class='doc-namespace-groups'><section class='inner'</section></section>",
            malformedNestedOpen);
        Assert.Equal(
            "<section class=\"doc-namespace-intro\"><p>Intro</p></section><section class='doc-namespace-groups'><section></section>",
            unbalancedNested);
    }

    [Fact]
    public void ExtractNamespaceNameFromReadmePath_ShouldReturnNull_WhenPathIsNotReadme()
    {
        // Act
        var namespaceName = DocAggregator.ExtractNamespaceNameFromReadmePath("docs/ForgeTrust.Web/NOTES.md");

        // Assert
        Assert.Null(namespaceName);
    }

    [Fact]
    public async Task Constructor_ShouldFallbackToDiscoveredRepositoryRoot_WhenSourceRootIsMissing()
    {
        // Arrange
        var localOptions = new RazorDocsOptions();
        var localEnv = A.Fake<IWebHostEnvironment>();
        var contentRoot = Path.Combine(Path.GetTempPath(), "repo-fallback-root");
        A.CallTo(() => localEnv.ContentRootPath).Returns(contentRoot);
        var expectedRoot = ForgeTrust.Runnable.Core.PathUtils.FindRepositoryRoot(contentRoot);
        string? capturedRoot = null;
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Invokes((string root, CancellationToken _) => capturedRoot = root)
            .Returns(Array.Empty<DocNode>());
        var aggregator = new DocAggregator(
            new[] { _harvesterFake },
            localOptions,
            localEnv,
            _memo,
            _sanitizerFake,
            _loggerFake);

        // Act
        _ = await aggregator.GetDocsAsync();

        // Assert
        Assert.Equal(expectedRoot, capturedRoot);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenConfiguredRepositoryRootIsWhitespace()
    {
        var options = new RazorDocsOptions
        {
            Source = new RazorDocsSourceOptions { RepositoryRoot = "   " }
        };

        var ex = Assert.Throws<ArgumentException>(
            () => new DocAggregator(
                new[] { _harvesterFake },
                options,
                _envFake,
                _memo,
                _sanitizerFake,
                _loggerFake));

        Assert.Equal(nameof(RazorDocsSourceOptions.RepositoryRoot), ex.ParamName);
        Assert.Contains("whitespace", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenBundleModeIsRequestedBeforeItIsImplemented()
    {
        var options = new RazorDocsOptions
        {
            Mode = RazorDocsMode.Bundle,
            Bundle = new RazorDocsBundleOptions { Path = "/tmp/docs.bundle.json" }
        };

        var ex = Assert.Throws<NotSupportedException>(
            () => new DocAggregator(
                new[] { _harvesterFake },
                options,
                _envFake,
                _memo,
                _sanitizerFake,
                _loggerFake));

        Assert.Contains("bundle mode", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenModeIsUnsupported()
    {
        var options = new RazorDocsOptions
        {
            Mode = (RazorDocsMode)999
        };

        var ex = Assert.Throws<NotSupportedException>(
            () => new DocAggregator(
                new[] { _harvesterFake },
                options,
                _envFake,
                _memo,
                _sanitizerFake,
                _loggerFake));

        Assert.Contains("Unsupported RazorDocs mode", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSourceOptionsAreNullInSourceMode()
    {
        var options = new RazorDocsOptions
        {
            Source = null!
        };

        var ex = Assert.Throws<ArgumentNullException>(
            () => new DocAggregator(
                new[] { _harvesterFake },
                options,
                _envFake,
                _memo,
                _sanitizerFake,
                _loggerFake));

        Assert.Equal("Source", ex.ParamName);
    }

    [Fact]
    public async Task GetDocByPathAsync_ShouldUsePathFallback_WhenCachedCanonicalPathIsNull()
    {
        // Arrange
        var harvestedDocs = new List<DocNode> { new("Method", "docs/service.cs#DoWork", "content") };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(harvestedDocs);

        // Act
        var result = await _aggregator.GetDocByPathAsync("DOCS/service.cs#DoWork");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Method", result!.Title);
    }

    [Fact]
    public async Task GetDocByPathAsync_ShouldMatchNormalizedSourcePath_WhenCanonicalPathDoesNotMatch()
    {
        var harvestedDocs = new List<DocNode> { new("Guide", "docs/guide.md", "<p>Guide</p>") };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(harvestedDocs);

        var result = await _aggregator.GetDocByPathAsync("/DOCS/guide.md/");

        Assert.NotNull(result);
        Assert.Equal("Guide", result!.Title);
    }

    [Fact]
    public async Task BuildCanonicalPath_ShouldNotAppendHtml_WhenSourceAlreadyHtml()
    {
        // Arrange
        var harvestedDocs = new List<DocNode>
        {
            new("AlreadyHtml", "docs/page.html", "content"),
            new("RootHtml", "index.html", "content")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(harvestedDocs);

        // Act
        var docs = await _aggregator.GetDocsAsync();

        // Assert
        Assert.Contains(docs, d => d.Path == "docs/page.html" && d.CanonicalPath == "docs/page.html");
        Assert.Contains(docs, d => d.Path == "index.html" && d.CanonicalPath == "index.html");
    }

    [Fact]
    public async Task GetDocsAsync_ShouldMergeNamespaceReadmes_WhenNamespaceNodesContainDuplicates()
    {
        // Arrange
        var namespaceContent = "<section class='doc-namespace-groups'><h4>Namespaces</h4></section>";
        var harvestedDocs = new List<DocNode>
        {
            new("Web", "Namespaces/ForgeTrust.Web", namespaceContent),
            new("Web-duplicate", "Namespaces/ForgeTrust.Web", namespaceContent),
            new("README", "docs/ForgeTrust.Web/README.md", "<p>Namespace intro</p>")
        };
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._)).Returns(harvestedDocs);

        // Act
        var docs = await _aggregator.GetDocsAsync();

        // Assert
        var namespaceDoc = docs.Single(d => d.Path == "Namespaces/ForgeTrust.Web");
        Assert.Contains("doc-namespace-intro", namespaceDoc.Content);
        Assert.DoesNotContain(docs, d => d.Path == "docs/ForgeTrust.Web/README.md");
    }

    public void Dispose()
    {
        (_memo as IDisposable)?.Dispose();
        _cache.Dispose();
    }
}
