using FakeItEasy;
using ForgeTrust.AppSurface.Caching;
using ForgeTrust.AppSurface.Docs.Models;
using ForgeTrust.AppSurface.Docs.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace ForgeTrust.AppSurface.Docs.Tests;

public sealed class PythonDocHarvesterTests : IDisposable
{
    private readonly string _testRoot = Directory.CreateTempSubdirectory("appsurface-docs-python-").FullName;

    [Fact]
    public async Task HarvestAsync_ReturnsNoDocs_WhenPythonHarvestingIsDisabled()
    {
        await WriteAsync("worker.py", "__all__ = [\"run\"]\ndef run():\n    \"\"\"Run.\"\"\"\n");
        var options = CreateEnabledOptions("worker.py");
        options.Harvest.Python.Enabled = false;
        var harvester = CreateHarvester(options);

        var docs = await harvester.HarvestAsync(_testRoot);

        Assert.Empty(docs);
        Assert.Empty(GetDiagnostics(harvester));
    }

    [Fact]
    public async Task HarvestAsync_EmitsGuidanceAndDoesNotReadSources_WhenPythonIncludeIsMissing()
    {
        await WriteAsync("worker.py", "__all__ = [\"run\"]\ndef run():\n    \"\"\"Run.\"\"\"\n");
        var harvester = CreateHarvester(new AppSurfaceDocsOptions());

        var docs = await harvester.HarvestAsync(_testRoot);

        Assert.Empty(docs);
        var diagnostic = Assert.Single(GetDiagnostics(harvester));
        Assert.Equal(DocHarvestDiagnosticCodes.PythonMissingInclude, diagnostic.Code);
        Assert.Contains("IncludeGlobs", diagnostic.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestAsync_PublishesLiteralExportsAndTheirDocumentedMethods_WithoutExecutingPython()
    {
        await WriteAsync(
            "sidecar/worker.py",
            """"
            """Worker module.

                A static Python sidecar.
            """
            __all__ = ["Worker", "deliver"]

            class Worker:
                """Runs one work item.

                    The indentation is normalized.
                """

                async def run(self):
                    """Runs asynchronously."""

                def internal_method(self):
                    """This member remains below its exported class."""

            async def deliver():
                """Delivers work."""

            def internal_helper():
                """Must not be published."""
            """");
        var harvester = CreateHarvester(CreateEnabledOptions("sidecar/**/*.py"));

        var docs = await harvester.HarvestAsync(_testRoot);

        var module = Assert.Single(docs, document => document.Path == "api/python/sidecar-worker");
        Assert.Equal("sidecar.worker Python API", module.Title);
        Assert.Contains("Worker module.<br /><br />A static Python sidecar.", module.Content, StringComparison.Ordinal);
        Assert.Contains("Runs one work item.<br /><br />The indentation is normalized.", module.Content, StringComparison.Ordinal);
        Assert.Contains(docs, document => document.Path == "api/python/sidecar-worker#class-worker");
        Assert.Contains(docs, document => document.Path == "api/python/sidecar-worker#async-function-deliver");
        Assert.Contains(docs, document => document.Path == "api/python/sidecar-worker#async-method-class-worker-run");
        Assert.Contains(docs, document => document.Path == "api/python/sidecar-worker#method-class-worker-internal-method");
        Assert.DoesNotContain(docs, document => document.Title.Contains("internal_helper", StringComparison.Ordinal));
        Assert.Equal("python", module.Metadata?.CodeLanguage);
        Assert.Equal("sidecar/worker.py", Assert.Single(module.SymbolSourceProvenance!, source => source.AnchorId == "class-worker").SourcePath);
        Assert.Empty(GetDiagnostics(harvester));
    }

    [Fact]
    public async Task HarvestAsync_UsesLastPythonRedefinitionAndPublishesEachExportOnce()
    {
        await WriteAsync(
            "worker.py",
            """"
            __all__ = ["Worker", "Worker", "run", "run"]

            class Worker:
                """Stale worker."""

                def execute(self):
                    """Stale method."""

            class Worker:
                """Final worker."""

                def execute(self):
                    """Final method."""

            def run():
                """Stale run."""

            def run():
                """Final run."""
            """");
        var harvester = CreateHarvester(CreateEnabledOptions("worker.py"));

        var docs = await harvester.HarvestAsync(_testRoot);

        var module = Assert.Single(docs, document => document.Path == "api/python/worker");
        Assert.Contains("Final worker.", module.Content, StringComparison.Ordinal);
        Assert.Contains("Final method.", module.Content, StringComparison.Ordinal);
        Assert.Contains("Final run.", module.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Stale", module.Content, StringComparison.Ordinal);
        Assert.Single(docs, document => document.Path == "api/python/worker#class-worker");
        Assert.Single(docs, document => document.Path == "api/python/worker#method-class-worker-execute");
        Assert.Single(docs, document => document.Path == "api/python/worker#function-run");
        Assert.Empty(GetDiagnostics(harvester));
    }

    [Fact]
    public async Task HarvestAsync_RejectsMissingAndDynamicPublicBoundaries()
    {
        await WriteAsync("missing.py", "def visible():\n    \"\"\"Visible.\"\"\"\n");
        await WriteAsync("dynamic.py", "names = [\"visible\"]\n__all__ = names\ndef visible():\n    \"\"\"Visible.\"\"\"\n");
        var harvester = CreateHarvester(CreateEnabledOptions("*.py"));

        var docs = await harvester.HarvestAsync(_testRoot);

        Assert.Empty(docs);
        var diagnostics = GetDiagnostics(harvester);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DocHarvestDiagnosticCodes.PythonPublicBoundaryMissing);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DocHarvestDiagnosticCodes.PythonPublicBoundaryInvalid);
    }

    [Fact]
    public async Task HarvestAsync_SkipsMalformedPythonBeforePublishingARecoveredTree()
    {
        await WriteAsync(
            "broken.py",
            """"
            __all__ = ["run"

            def run():
                """Would be published if the module parsed."""
            """");
        var harvester = CreateHarvester(CreateEnabledOptions("broken.py"));

        var docs = await harvester.HarvestAsync(_testRoot);

        Assert.Empty(docs);
        var diagnostic = Assert.Single(GetDiagnostics(harvester));
        Assert.Equal(DocHarvestDiagnosticCodes.PythonParseFailed, diagnostic.Code);
        Assert.Contains("broken.py", diagnostic.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HarvestAsync_ReportsUnsupportedAndUnknownExportNames()
    {
        await WriteAsync(
            "exports.py",
            """"
            __all__ = ["constant", "missing", "run"]
            constant = "value"

            def run():
                """Run."""
            """");
        var harvester = CreateHarvester(CreateEnabledOptions("exports.py"));

        var docs = await harvester.HarvestAsync(_testRoot);

        Assert.Contains(docs, document => document.Path == "api/python/exports#function-run");
        var diagnostics = GetDiagnostics(harvester);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DocHarvestDiagnosticCodes.PythonExportNotSupported);
        Assert.Contains(diagnostics, diagnostic => diagnostic.Code == DocHarvestDiagnosticCodes.PythonExportNotFound);
    }

    [Fact]
    public async Task HarvestAsync_IgnoresAnOtherwiseEmptyPackageInitializerWithoutBoundaryGuidance()
    {
        await WriteAsync("sidecar/__init__.py", "\"\"\"Package description.\"\"\"\n");
        var harvester = CreateHarvester(CreateEnabledOptions("sidecar/**/*.py"));

        var docs = await harvester.HarvestAsync(_testRoot);

        Assert.Empty(docs);
        Assert.Empty(GetDiagnostics(harvester));
    }

    [Fact]
    public async Task HarvestAsync_ReportsOversizedPythonSourceBeforeParsing()
    {
        await WriteAsync("worker.py", "__all__ = [\"run\"]\ndef run():\n    \"\"\"Run.\"\"\"\n");
        var options = CreateEnabledOptions("worker.py");
        options.Harvest.Python.MaxFileSizeBytes = 10;
        var harvester = CreateHarvester(options);

        var docs = await harvester.HarvestAsync(_testRoot);

        Assert.Empty(docs);
        Assert.Contains(GetDiagnostics(harvester), diagnostic => diagnostic.Code == DocHarvestDiagnosticCodes.PythonFileTooLarge);
    }

    [Fact]
    public async Task GetSearchIndexPayloadAsync_ProjectsPythonSymbolsAsGeneratedApiEntries()
    {
        await WriteAsync(
            "sidecar/worker.py",
            """"
            __all__ = ["deliver"]

            def deliver():
                """Delivers a work item."""
            """");
        var options = CreateEnabledOptions("sidecar/**/*.py");
        options.Source.RepositoryRoot = _testRoot;
        options.Contributor.Enabled = false;
        var environment = A.Fake<IWebHostEnvironment>();
        A.CallTo(() => environment.ContentRootPath).Returns(_testRoot);
        var aggregator = new DocAggregator(
            [CreateHarvester(options)],
            options,
            environment,
            new Memo(new MemoryCache(new MemoryCacheOptions())),
            new AppSurfaceDocsHtmlSanitizer(),
            NullLogger<DocAggregator>.Instance);

        var payload = await aggregator.GetSearchIndexPayloadAsync();

        var symbol = Assert.Single(payload.Documents, document => document.Title == "deliver");
        Assert.Equal("/docs/api/python/sidecar-worker#function-deliver", symbol.Path);
        Assert.Equal("python-function", symbol.PageType);
        Assert.Equal("python", symbol.Language);
        Assert.Equal("Python", symbol.LanguageLabel);
        Assert.Equal("public", symbol.ApiLifecycle);
        Assert.Equal("Public API", symbol.ApiLifecycleLabel);
        Assert.False(symbol.IsDeprecated);
        Assert.True(symbol.IsGeneratedApiSymbol);
        Assert.Contains("Delivers a work item.", symbol.BodyText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetDocsAsync_LinksAcceptedPythonModuleToItsDocumentedCSharpHost()
    {
        await WriteAsync(
            "sidecar/worker.py",
            """"
            __all__ = ["deliver"]

            def deliver():
                """Delivers a work item."""
            """");
        await WriteAsync(
            "Host.cs",
            """"
            using ForgeTrust.AppSurface.Docs;

            namespace Sample.Host;

            /// <summary>Hosts the worker sidecar.</summary>
            [AppSurfacePythonModule("sidecar/worker.py")]
            public sealed class WorkerHost;
            """");
        var options = CreateEnabledOptions("sidecar/**/*.py");
        options.Source.RepositoryRoot = _testRoot;
        options.Contributor.Enabled = false;
        var environment = A.Fake<IWebHostEnvironment>();
        A.CallTo(() => environment.ContentRootPath).Returns(_testRoot);
        var aggregator = new DocAggregator(
            [
                new CSharpDocHarvester(options, NullLogger<CSharpDocHarvester>.Instance),
                CreateHarvester(options)
            ],
            options,
            environment,
            new Memo(new MemoryCache(new MemoryCacheOptions())),
            new AppSurfaceDocsHtmlSanitizer(),
            NullLogger<DocAggregator>.Instance);

        var docs = await aggregator.GetDocsAsync();

        var pythonModule = Assert.Single(docs, document => document.Path == "api/python/sidecar-worker");
        var csharpHost = Assert.Single(docs, document => document.Path == "Namespaces/Sample.Host");
        Assert.Contains("C# host:", pythonModule.Content, StringComparison.Ordinal);
        Assert.Contains("href=\"/docs/Namespaces/Sample.Host.html#Sample-Host-WorkerHost\"", pythonModule.Content, StringComparison.Ordinal);
        Assert.Contains(">WorkerHost</a>", pythonModule.Content, StringComparison.Ordinal);
        Assert.Contains("Python module:", csharpHost.Content, StringComparison.Ordinal);
        Assert.Contains("href=\"/docs/api/python/sidecar-worker\"", csharpHost.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("data-appsurfacedocs-python-", pythonModule.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("data-appsurfacedocs-python-", csharpHost.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void AppSurfaceDocsOptionsValidator_RejectsNonPositivePythonInputBudget()
    {
        var options = CreateEnabledOptions("worker.py");
        options.Harvest.Python.MaxFileSizeBytes = 0;

        var validation = new AppSurfaceDocsOptionsValidator().Validate(null, options);

        Assert.False(validation.Succeeded);
        Assert.Contains("AppSurfaceDocs:Harvest:Python:MaxFileSizeBytes must be greater than zero.", validation.Failures!);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    private static AppSurfaceDocsOptions CreateEnabledOptions(params string[] includeGlobs)
    {
        var options = new AppSurfaceDocsOptions();
        options.Harvest.Python.IncludeGlobs = includeGlobs;
        return options;
    }

    private static PythonDocHarvester CreateHarvester(AppSurfaceDocsOptions options) =>
        new(options, NullLogger<PythonDocHarvester>.Instance);

    private static IReadOnlyList<DocHarvestDiagnostic> GetDiagnostics(PythonDocHarvester harvester) =>
        ((IDocHarvesterDiagnosticProvider)harvester).GetHarvestDiagnostics();

    private async Task WriteAsync(string relativePath, string content)
    {
        var path = TestPathUtils.PathUnder(_testRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }
}
