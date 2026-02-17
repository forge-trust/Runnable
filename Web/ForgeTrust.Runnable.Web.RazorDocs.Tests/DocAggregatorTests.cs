using AngleSharp;
using FakeItEasy;
using ForgeTrust.Runnable.Caching;
using Ganss.Xss;
using ForgeTrust.Runnable.Web.RazorDocs.Models;
using ForgeTrust.Runnable.Web.RazorDocs.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Ext_IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace ForgeTrust.Runnable.Web.RazorDocs.Tests;

public class DocAggregatorTests : IDisposable
{
    private readonly IDocHarvester _harvesterFake;
    private readonly Ext_IConfiguration _configFake;
    private readonly IWebHostEnvironment _envFake;
    private readonly ILogger<DocAggregator> _loggerFake;
    private readonly IHtmlSanitizer _sanitizerFake;
    private readonly IMemoryCache _cache;
    private readonly IMemo _memo;
    private readonly DocAggregator _aggregator;

    public DocAggregatorTests()
    {
        _harvesterFake = A.Fake<IDocHarvester>();
        _configFake = A.Fake<Ext_IConfiguration>();
        _envFake = A.Fake<IWebHostEnvironment>();
        _loggerFake = A.Fake<ILogger<DocAggregator>>();
        _sanitizerFake = A.Fake<IHtmlSanitizer>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _memo = new Memo(_cache);

        A.CallTo(() => _envFake.ContentRootPath).Returns(Path.GetTempPath());

        // Default: just return input for sanitization in most tests
        A.CallTo(() => _sanitizerFake.Sanitize(A<string>._, A<string>.Ignored, A<IMarkupFormatter>.Ignored))
            .ReturnsLazily((string input, string _, IMarkupFormatter _) => input);

        _aggregator = new DocAggregator(
            new[] { _harvesterFake },
            _configFake,
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
        var result = (await _aggregator.GetDocsAsync()).ToList();

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
        var firstResult = (await _aggregator.GetDocsAsync()).ToList();
        var secondResult = (await _aggregator.GetDocsAsync()).ToList();

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
        A.CallTo(() => _sanitizerFake.Sanitize(unsafeHtml, A<string>.Ignored, A<IMarkupFormatter>.Ignored))
            .Returns(safeHtml);

        // Act
        var result = (await _aggregator.GetDocsAsync()).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(safeHtml, result.First().Content);
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
        var result = (await _aggregator.GetDocsAsync()).ToList();

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
            _configFake,
            _envFake,
            _memo,
            _sanitizerFake,
            _loggerFake
        );

        // Act
        var result = (await aggregator.GetDocsAsync()).ToList();

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
            A.Fake<Ext_IConfiguration>(),
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
        var docs = (await _aggregator.GetDocsAsync()).ToList();
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
        A.CallTo(() => _configFake["RepositoryRoot"]).Returns(configuredRoot);
        string? capturedRoot = null;
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Invokes((string root, CancellationToken _) => capturedRoot = root)
            .Returns(Enumerable.Empty<DocNode>());
        var aggregator = new DocAggregator(
            new[] { _harvesterFake },
            _configFake,
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
        var docs = (await _aggregator.GetDocsAsync()).ToList();

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
    public async Task GetDocsAsync_ShouldPropagateOperationCanceled_FromHarvester()
    {
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Throws(new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _aggregator.GetDocsAsync());
    }

    [Fact]
    public async Task GetDocsAsync_ShouldCancelCallerWait_WithoutPoisoningSharedSnapshot()
    {
        // Arrange
        var harvestedDocs = new List<DocNode> { new("Recovered", "path", "content") };
        var releaseHarvester = new TaskCompletionSource<IEnumerable<DocNode>>(TaskCreationOptions.RunContinuationsAsynchronously);
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
        var recovered = (await _aggregator.GetDocsAsync()).ToList();

        // Assert
        Assert.NotNull(observedToken);
        Assert.False(observedToken!.Value.CanBeCanceled);
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

        var docs = (await _aggregator.GetDocsAsync()).ToList();

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

        var docs = (await _aggregator.GetDocsAsync()).ToList();

        Assert.Contains(docs, d => d.Path == "docs/readme.md" && d.CanonicalPath == "docs/readme.md.html");
        Assert.Contains(docs, d => d.Path == "docs/readme_md.md" && d.CanonicalPath == "docs/readme_md.md.html");
        Assert.Contains(docs, d => d.Path == "docs/api.v2.md" && d.CanonicalPath == "docs/api.v2.md.html");
        Assert.NotEqual("docs/readme.md.html", "docs/readme_md.md.html");
    }

    [Fact]
    public async Task Constructor_ShouldPreferConfiguredRoot_OverEnvironmentFallback()
    {
        var configuredRoot = Path.Combine(Path.GetTempPath(), "configured-root");
        var localConfig = A.Fake<Ext_IConfiguration>();
        A.CallTo(() => localConfig["RepositoryRoot"]).Returns(configuredRoot);
        var localEnv = A.Fake<IWebHostEnvironment>();
        A.CallTo(() => localEnv.ContentRootPath).Returns("/definitely/not/used");
        string? capturedRoot = null;
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Invokes((string root, CancellationToken _) => capturedRoot = root)
            .Returns(Enumerable.Empty<DocNode>());
        var aggregator = new DocAggregator(
            new[] { _harvesterFake },
            localConfig,
            localEnv,
            _memo,
            _sanitizerFake,
            _loggerFake);

        _ = await aggregator.GetDocsAsync();

        Assert.Equal(configuredRoot, capturedRoot);
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
        var docs = (await _aggregator.GetDocsAsync()).ToList();

        // Assert
        var namespaceDoc = docs.Single(d => d.Path == "Namespaces/ForgeTrust.Web");
        Assert.DoesNotContain(docs, d => d.Path == "docs/ForgeTrust.Web/README.md");
        Assert.Contains("doc-namespace-intro", namespaceDoc.Content);
        Assert.Contains("<p>Namespace intro</p>", namespaceDoc.Content);
        Assert.Contains("</section><section class=\"doc-namespace-intro\">", namespaceDoc.Content);
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
    public async Task Constructor_ShouldFallbackToDiscoveredRepositoryRoot_WhenConfigIsMissing()
    {
        // Arrange
        var localConfig = A.Fake<Ext_IConfiguration>();
        A.CallTo(() => localConfig["RepositoryRoot"]).Returns(null);
        var localEnv = A.Fake<IWebHostEnvironment>();
        var contentRoot = Path.Combine(Path.GetTempPath(), "repo-fallback-root");
        A.CallTo(() => localEnv.ContentRootPath).Returns(contentRoot);
        var expectedRoot = ForgeTrust.Runnable.Core.PathUtils.FindRepositoryRoot(contentRoot);
        string? capturedRoot = null;
        A.CallTo(() => _harvesterFake.HarvestAsync(A<string>._, A<CancellationToken>._))
            .Invokes((string root, CancellationToken _) => capturedRoot = root)
            .Returns(Enumerable.Empty<DocNode>());
        var aggregator = new DocAggregator(
            new[] { _harvesterFake },
            localConfig,
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
        var docs = (await _aggregator.GetDocsAsync()).ToList();

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
        var docs = (await _aggregator.GetDocsAsync()).ToList();

        // Assert
        var namespaceDoc = docs.Single(d => d.Path == "Namespaces/ForgeTrust.Web");
        Assert.Contains("doc-namespace-intro", namespaceDoc.Content);
        Assert.DoesNotContain(docs, d => d.Path == "docs/ForgeTrust.Web/README.md");
    }

    public void Dispose()
    {
        if (_memo is IDisposable disposableMemo)
        {
            disposableMemo.Dispose();
        }

        _cache.Dispose();
    }
}
