using System.Text;

namespace ForgeTrust.Runnable.PackageIndex.Tests;

public sealed class PackageIndexGeneratorTests : IDisposable
{
    private readonly string _repositoryRoot;

    public PackageIndexGeneratorTests()
    {
        _repositoryRoot = Path.Combine(Path.GetTempPath(), "PackageIndexTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_repositoryRoot);
    }

    [Fact]
    public async Task GenerateAsync_ThrowsWhenCandidateProjectIsMissingFromManifest()
    {
        await WriteFileAsync("packages/README.md.yml", "title: Runnable");
        await WriteFileAsync(
            "packages/package-index.yml",
            """
            packages:
              - project: Console/ForgeTrust.Runnable.Console/ForgeTrust.Runnable.Console.csproj
                classification: public
                order: 10
                use_when: Start here for CLI apps.
                includes: Command hosting.
                does_not_include: Web hosting.
                start_here_path: Console/ForgeTrust.Runnable.Console/README.md
            """);
        await WriteFileAsync("Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj", "<Project />");
        await WriteFileAsync("Console/ForgeTrust.Runnable.Console/ForgeTrust.Runnable.Console.csproj", "<Project />");
        await WriteFileAsync("Console/ForgeTrust.Runnable.Console/README.md", "# Console");

        var generator = CreateGenerator(new Dictionary<string, PackageProjectMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["Console/ForgeTrust.Runnable.Console/ForgeTrust.Runnable.Console.csproj"] = CreateMetadata(
                "Console/ForgeTrust.Runnable.Console/ForgeTrust.Runnable.Console.csproj",
                "ForgeTrust.Runnable.Console"),
            ["Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj",
                "ForgeTrust.Runnable.Web")
        });

        var request = CreateRequest();
        var error = await Assert.ThrowsAsync<PackageIndexException>(() => generator.GenerateAsync(request));

        Assert.Contains("missing a classification", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAsync_ThrowsWhenPublicPackageGuidanceIsMissing()
    {
        await WriteFileAsync("packages/README.md.yml", "title: Runnable");
        await WriteFileAsync(
            "packages/package-index.yml",
            """
            packages:
              - project: Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj
                classification: public
                order: 10
                includes: Base startup
                does_not_include: OpenAPI
                start_here_path: Web/ForgeTrust.Runnable.Web/README.md
            """);
        await WriteFileAsync("Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj", "<Project />");
        await WriteFileAsync("Web/ForgeTrust.Runnable.Web/README.md", "# Web");

        var generator = CreateGenerator(new Dictionary<string, PackageProjectMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj",
                "ForgeTrust.Runnable.Web")
        });

        var error = await Assert.ThrowsAsync<PackageIndexException>(() => generator.GenerateAsync(CreateRequest()));

        Assert.Contains("UseWhen", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_ThrowsWhenStartHereDocIsMissing()
    {
        await WriteFileAsync("packages/README.md.yml", "title: Runnable");
        await WriteFileAsync(
            "packages/package-index.yml",
            """
            packages:
              - project: Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj
                classification: public
                order: 10
                use_when: Install this first for a normal ASP.NET Core app with Runnable modules.
                includes: Base web startup.
                does_not_include: OpenAPI.
                start_here_path: Web/ForgeTrust.Runnable.Web/README.md
            """);
        await WriteFileAsync("Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj", "<Project />");

        var generator = CreateGenerator(new Dictionary<string, PackageProjectMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj",
                "ForgeTrust.Runnable.Web")
        });

        var error = await Assert.ThrowsAsync<PackageIndexException>(() => generator.GenerateAsync(CreateRequest()));

        Assert.Contains("missing documentation", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAsync_ThrowsWhenDependencyReferenceIsUnknown()
    {
        await WriteFileAsync("packages/README.md.yml", "title: Runnable");
        await WriteFileAsync(
            "packages/package-index.yml",
            """
            packages:
              - project: Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj
                classification: public
                order: 10
                use_when: Install this first for a normal ASP.NET Core app with Runnable modules.
                includes: Base web startup.
                does_not_include: OpenAPI.
                start_here_path: Web/ForgeTrust.Runnable.Web/README.md
                depends_on:
                  - Missing.Package
            """);
        await WriteFileAsync("Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj", "<Project />");
        await WriteFileAsync("Web/ForgeTrust.Runnable.Web/README.md", "# Web");

        var generator = CreateGenerator(new Dictionary<string, PackageProjectMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj",
                "ForgeTrust.Runnable.Web")
        });

        var error = await Assert.ThrowsAsync<PackageIndexException>(() => generator.GenerateAsync(CreateRequest()));

        Assert.Contains("unknown package id", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAsync_ThrowsWhenRazorWireCliIsNotExcluded()
    {
        await WriteFileAsync("packages/README.md.yml", "title: Runnable");
        await WriteFileAsync(
            "packages/package-index.yml",
            """
            packages:
              - project: Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj
                classification: public
                order: 10
                use_when: Install this first for a normal ASP.NET Core app with Runnable modules.
                includes: Base web startup.
                does_not_include: OpenAPI.
                start_here_path: Web/ForgeTrust.Runnable.Web/README.md
              - project: Web/ForgeTrust.Runnable.Web.RazorWire.Cli/ForgeTrust.Runnable.Web.RazorWire.Cli.csproj
                classification: proof_host
                order: 20
                note: Incorrect classification for the CLI.
            """);
        await WriteFileAsync("Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj", "<Project />");
        await WriteFileAsync("Web/ForgeTrust.Runnable.Web/README.md", "# Web");
        await WriteFileAsync("Web/ForgeTrust.Runnable.Web.RazorWire.Cli/ForgeTrust.Runnable.Web.RazorWire.Cli.csproj", "<Project />");

        var generator = CreateGenerator(new Dictionary<string, PackageProjectMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj",
                "ForgeTrust.Runnable.Web"),
            ["Web/ForgeTrust.Runnable.Web.RazorWire.Cli/ForgeTrust.Runnable.Web.RazorWire.Cli.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web.RazorWire.Cli/ForgeTrust.Runnable.Web.RazorWire.Cli.csproj",
                "ForgeTrust.Runnable.Web.RazorWire.Cli",
                outputType: "Exe")
        });

        var error = await Assert.ThrowsAsync<PackageIndexException>(() => generator.GenerateAsync(CreateRequest()));

        Assert.Contains("must stay excluded", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAsync_RendersChooserSectionsAndInstallCommands()
    {
        await WriteCommonChooserFilesAsync(includeUnreleased: true);

        var generator = CreateGenerator(new Dictionary<string, PackageProjectMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj",
                "ForgeTrust.Runnable.Web"),
            ["Web/ForgeTrust.Runnable.Web.OpenApi/ForgeTrust.Runnable.Web.OpenApi.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web.OpenApi/ForgeTrust.Runnable.Web.OpenApi.csproj",
                "ForgeTrust.Runnable.Web.OpenApi"),
            ["Web/ForgeTrust.Runnable.Web.Tailwind/runtimes/ForgeTrust.Runnable.Web.Tailwind.Runtime.osx-arm64.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web.Tailwind/runtimes/ForgeTrust.Runnable.Web.Tailwind.Runtime.osx-arm64.csproj",
                "ForgeTrust.Runnable.Web.Tailwind.Runtime.osx-arm64"),
            ["Web/ForgeTrust.Runnable.Web.RazorDocs/ForgeTrust.Runnable.Web.RazorDocs.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web.RazorDocs/ForgeTrust.Runnable.Web.RazorDocs.csproj",
                "ForgeTrust.Runnable.Web.RazorDocs"),
            ["Web/ForgeTrust.Runnable.Web.RazorWire.Cli/ForgeTrust.Runnable.Web.RazorWire.Cli.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web.RazorWire.Cli/ForgeTrust.Runnable.Web.RazorWire.Cli.csproj",
                "ForgeTrust.Runnable.Web.RazorWire.Cli",
                outputType: "Exe")
        });

        var markdown = await generator.GenerateAsync(CreateRequest());

        Assert.Contains("# Runnable v0.1 package chooser", markdown, StringComparison.Ordinal);
        Assert.Contains("```bash", markdown, StringComparison.Ordinal);
        Assert.Contains("dotnet package add ForgeTrust.Runnable.Web", markdown, StringComparison.Ordinal);
        Assert.Contains("[examples/web-app/README.md](../examples/web-app/README.md)", markdown, StringComparison.Ordinal);
        Assert.Contains("| `ForgeTrust.Runnable.Web` |", markdown, StringComparison.Ordinal);
        Assert.Contains("### Support and runtime packages", markdown, StringComparison.Ordinal);
        Assert.Contains("### Docs and proof hosts", markdown, StringComparison.Ordinal);
        Assert.Contains("### Not in the direct-install matrix", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_RendersDistinctTargetFrameworkSummary()
    {
        await WriteFileAsync("packages/README.md.yml", "title: Runnable");
        await WriteFileAsync(
            "packages/package-index.yml",
            """
            packages:
              - project: Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj
                classification: public
                order: 10
                use_when: Install this first for a normal ASP.NET Core app with Runnable modules.
                includes: Base web startup.
                does_not_include: OpenAPI.
                start_here_path: Web/ForgeTrust.Runnable.Web/README.md
              - project: Console/ForgeTrust.Runnable.Console/ForgeTrust.Runnable.Console.csproj
                classification: public
                order: 20
                use_when: Start here for CLI apps.
                includes: Command hosting.
                does_not_include: Web hosting.
                start_here_path: Console/ForgeTrust.Runnable.Console/README.md
            """);
        await WriteFileAsync("Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj", "<Project />");
        await WriteFileAsync("Web/ForgeTrust.Runnable.Web/README.md", "# Web");
        await WriteFileAsync("Console/ForgeTrust.Runnable.Console/ForgeTrust.Runnable.Console.csproj", "<Project />");
        await WriteFileAsync("Console/ForgeTrust.Runnable.Console/README.md", "# Console");
        await WriteFileAsync("examples/web-app/README.md", "# Example");
        await WriteFileAsync("releases/README.md", "# Releases");
        await WriteFileAsync("releases/upgrade-policy.md", "# Policy");
        await WriteFileAsync("CHANGELOG.md", "# Changelog");

        var generator = CreateGenerator(new Dictionary<string, PackageProjectMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj",
                "ForgeTrust.Runnable.Web"),
            ["Console/ForgeTrust.Runnable.Console/ForgeTrust.Runnable.Console.csproj"] = CreateMetadata(
                "Console/ForgeTrust.Runnable.Console/ForgeTrust.Runnable.Console.csproj",
                "ForgeTrust.Runnable.Console",
                targetFramework: "net9.0")
        });

        var markdown = await generator.GenerateAsync(CreateRequest());

        Assert.Contains("`net10.0`", markdown, StringComparison.Ordinal);
        Assert.Contains("`net9.0`", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_RendersMissingUnreleasedStateExplicitly()
    {
        await WriteCommonChooserFilesAsync(includeUnreleased: false);

        var generator = CreateGenerator(new Dictionary<string, PackageProjectMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj",
                "ForgeTrust.Runnable.Web"),
            ["Web/ForgeTrust.Runnable.Web.OpenApi/ForgeTrust.Runnable.Web.OpenApi.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web.OpenApi/ForgeTrust.Runnable.Web.OpenApi.csproj",
                "ForgeTrust.Runnable.Web.OpenApi"),
            ["Web/ForgeTrust.Runnable.Web.Tailwind/runtimes/ForgeTrust.Runnable.Web.Tailwind.Runtime.osx-arm64.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web.Tailwind/runtimes/ForgeTrust.Runnable.Web.Tailwind.Runtime.osx-arm64.csproj",
                "ForgeTrust.Runnable.Web.Tailwind.Runtime.osx-arm64"),
            ["Web/ForgeTrust.Runnable.Web.RazorDocs/ForgeTrust.Runnable.Web.RazorDocs.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web.RazorDocs/ForgeTrust.Runnable.Web.RazorDocs.csproj",
                "ForgeTrust.Runnable.Web.RazorDocs"),
            ["Web/ForgeTrust.Runnable.Web.RazorWire.Cli/ForgeTrust.Runnable.Web.RazorWire.Cli.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web.RazorWire.Cli/ForgeTrust.Runnable.Web.RazorWire.Cli.csproj",
                "ForgeTrust.Runnable.Web.RazorWire.Cli",
                outputType: "Exe")
        });

        var markdown = await generator.GenerateAsync(CreateRequest());

        Assert.Contains("Not published yet", markdown, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyAsync_ThrowsWhenGeneratedReadmeIsStale()
    {
        await WriteCommonChooserFilesAsync(includeUnreleased: true);
        await WriteFileAsync("packages/README.md", "# stale");

        var generator = CreateGenerator(new Dictionary<string, PackageProjectMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj",
                "ForgeTrust.Runnable.Web"),
            ["Web/ForgeTrust.Runnable.Web.OpenApi/ForgeTrust.Runnable.Web.OpenApi.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web.OpenApi/ForgeTrust.Runnable.Web.OpenApi.csproj",
                "ForgeTrust.Runnable.Web.OpenApi"),
            ["Web/ForgeTrust.Runnable.Web.Tailwind/runtimes/ForgeTrust.Runnable.Web.Tailwind.Runtime.osx-arm64.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web.Tailwind/runtimes/ForgeTrust.Runnable.Web.Tailwind.Runtime.osx-arm64.csproj",
                "ForgeTrust.Runnable.Web.Tailwind.Runtime.osx-arm64"),
            ["Web/ForgeTrust.Runnable.Web.RazorDocs/ForgeTrust.Runnable.Web.RazorDocs.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web.RazorDocs/ForgeTrust.Runnable.Web.RazorDocs.csproj",
                "ForgeTrust.Runnable.Web.RazorDocs"),
            ["Web/ForgeTrust.Runnable.Web.RazorWire.Cli/ForgeTrust.Runnable.Web.RazorWire.Cli.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web.RazorWire.Cli/ForgeTrust.Runnable.Web.RazorWire.Cli.csproj",
                "ForgeTrust.Runnable.Web.RazorWire.Cli",
                outputType: "Exe")
        });

        var error = await Assert.ThrowsAsync<PackageIndexException>(() => generator.VerifyAsync(CreateRequest()));

        Assert.Contains("stale", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyAsync_ThrowsWhenGeneratedReadmeIsMissing()
    {
        await WriteCommonChooserFilesAsync(includeUnreleased: true);

        var generator = CreateGenerator(new Dictionary<string, PackageProjectMetadata>(StringComparer.OrdinalIgnoreCase)
        {
            ["Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj",
                "ForgeTrust.Runnable.Web"),
            ["Web/ForgeTrust.Runnable.Web.OpenApi/ForgeTrust.Runnable.Web.OpenApi.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web.OpenApi/ForgeTrust.Runnable.Web.OpenApi.csproj",
                "ForgeTrust.Runnable.Web.OpenApi"),
            ["Web/ForgeTrust.Runnable.Web.Tailwind/runtimes/ForgeTrust.Runnable.Web.Tailwind.Runtime.osx-arm64.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web.Tailwind/runtimes/ForgeTrust.Runnable.Web.Tailwind.Runtime.osx-arm64.csproj",
                "ForgeTrust.Runnable.Web.Tailwind.Runtime.osx-arm64"),
            ["Web/ForgeTrust.Runnable.Web.RazorDocs/ForgeTrust.Runnable.Web.RazorDocs.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web.RazorDocs/ForgeTrust.Runnable.Web.RazorDocs.csproj",
                "ForgeTrust.Runnable.Web.RazorDocs"),
            ["Web/ForgeTrust.Runnable.Web.RazorWire.Cli/ForgeTrust.Runnable.Web.RazorWire.Cli.csproj"] = CreateMetadata(
                "Web/ForgeTrust.Runnable.Web.RazorWire.Cli/ForgeTrust.Runnable.Web.RazorWire.Cli.csproj",
                "ForgeTrust.Runnable.Web.RazorWire.Cli",
                outputType: "Exe")
        });

        var error = await Assert.ThrowsAsync<PackageIndexException>(() => generator.VerifyAsync(CreateRequest()));

        Assert.Contains("Missing generated file", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAsync_ThrowsWhenChooserSidecarIsMissing()
    {
        await WriteFileAsync("packages/package-index.yml", "packages: []");

        var generator = CreateGenerator(new Dictionary<string, PackageProjectMetadata>(StringComparer.OrdinalIgnoreCase));
        var error = await Assert.ThrowsAsync<PackageIndexException>(() => generator.GenerateAsync(CreateRequest()));

        Assert.Contains("paired sidecar", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CommandLineOptions_Parse_UsesDefaultsAndOverrides()
    {
        var defaults = CommandLineOptions.Parse([], _repositoryRoot);

        Assert.Equal(Path.Combine(_repositoryRoot, "packages", "package-index.yml"), defaults.Request.ManifestPath);
        Assert.Equal(Path.Combine(_repositoryRoot, "packages", "README.md"), defaults.Request.OutputPath);

        var parsed = CommandLineOptions.Parse(
            ["--repo-root", "src", "--manifest", "manifest.yml", "--output", "chooser.md"],
            _repositoryRoot);

        Assert.Equal(Path.GetFullPath(Path.Combine(_repositoryRoot, "src")), parsed.Request.RepositoryRoot);
        Assert.Equal(Path.GetFullPath(Path.Combine(_repositoryRoot, "src", "manifest.yml")), parsed.Request.ManifestPath);
        Assert.Equal(Path.GetFullPath(Path.Combine(_repositoryRoot, "src", "chooser.md")), parsed.Request.OutputPath);
    }

    [Fact]
    public void CommandLineOptions_Parse_ThrowsWhenOptionValueIsMissing()
    {
        var error = Assert.Throws<PackageIndexException>(() => CommandLineOptions.Parse(["--manifest"], _repositoryRoot));

        Assert.Contains("requires a value", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_WritesUsageWhenNoCommandIsProvided()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await Program.RunAsync([], stdout, stderr, _repositoryRoot);

        Assert.Equal(1, exitCode);
        Assert.Contains("Usage:", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_WritesUsageWhenCommandIsUnknown()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await Program.RunAsync(["mystery"], stdout, stderr, _repositoryRoot);

        Assert.Equal(1, exitCode);
        Assert.Contains("Usage:", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void IsCandidateProject_ExcludesGeneratedAndToolingPaths()
    {
        Assert.False(PackageProjectScanner.IsCandidateProject("tools/ForgeTrust.Runnable.PackageIndex/ForgeTrust.Runnable.PackageIndex.csproj"));
        Assert.False(PackageProjectScanner.IsCandidateProject("examples/web-app/WebAppExample.csproj"));
        Assert.False(PackageProjectScanner.IsCandidateProject("Web/ForgeTrust.Runnable.Web.RazorWire.IntegrationTests/ForgeTrust.Runnable.Web.RazorWire.IntegrationTests.csproj"));
        Assert.False(PackageProjectScanner.IsCandidateProject("Config/ForgeTrust.Runnable.Config.Tests/ForgeTrust.Runnable.Config.Tests.csproj"));
        Assert.True(PackageProjectScanner.IsCandidateProject("Web/ForgeTrust.Runnable.Web.RazorDocs.Standalone/ForgeTrust.Runnable.Web.RazorDocs.Standalone.csproj"));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DotNetProjectMetadataProvider_ParsesRealMsbuildOutput()
    {
        await WriteFileAsync(
            "src/Dependency/Dependency.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        await WriteFileAsync(
            "src/App/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../Dependency/Dependency.csproj" />
              </ItemGroup>
            </Project>
            """);

        var provider = new DotNetProjectMetadataProvider();
        var metadata = await provider.GetMetadataAsync(_repositoryRoot, "src/App/App.csproj", CancellationToken.None);

        Assert.Equal("App", metadata.PackageId);
        Assert.Equal("net10.0", metadata.TargetFramework);
        Assert.Equal("Exe", metadata.OutputType);
        Assert.Single(metadata.ProjectReferences);
        Assert.EndsWith("src/Dependency/Dependency.csproj", metadata.ProjectReferences[0].Replace('\\', '/'), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DotNetProjectMetadataProvider_ThrowsWhenProjectCannotBeEvaluated()
    {
        var provider = new DotNetProjectMetadataProvider();

        var error = await Assert.ThrowsAsync<PackageIndexException>(
            () => provider.GetMetadataAsync(_repositoryRoot, "missing/Nope.csproj", CancellationToken.None));

        Assert.Contains("Failed to evaluate", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_repositoryRoot))
        {
            Directory.Delete(_repositoryRoot, recursive: true);
        }
    }

    private PackageIndexGenerator CreateGenerator(IReadOnlyDictionary<string, PackageProjectMetadata> metadataByProject)
    {
        return new PackageIndexGenerator(
            new PackageProjectScanner(),
            new FakeMetadataProvider(metadataByProject),
            new PackageManifestLoader());
    }

    private PackageIndexRequest CreateRequest()
    {
        return new PackageIndexRequest(
            _repositoryRoot,
            Path.Combine(_repositoryRoot, "packages", "package-index.yml"),
            Path.Combine(_repositoryRoot, "packages", "README.md"));
    }

    private async Task WriteCommonChooserFilesAsync(bool includeUnreleased)
    {
        await WriteFileAsync(
            "packages/README.md.yml",
            """
            title: Runnable v0.1 package chooser
            """);
        await WriteFileAsync(
            "packages/package-index.yml",
            """
            packages:
              - project: Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj
                classification: public
                order: 10
                use_when: Install this first for a normal ASP.NET Core app with Runnable modules.
                includes: Base web startup, middleware composition, and endpoint registration.
                does_not_include: OpenAPI, hosted API docs UI, and Tailwind asset compilation.
                start_here_path: Web/ForgeTrust.Runnable.Web/README.md
                recipe_summary: Add `ForgeTrust.Runnable.Web.OpenApi` after `ForgeTrust.Runnable.Web` when you want an OpenAPI document.
              - project: Web/ForgeTrust.Runnable.Web.OpenApi/ForgeTrust.Runnable.Web.OpenApi.csproj
                classification: public
                order: 20
                use_when: Add this after the base web package when you want an OpenAPI document.
                includes: OpenAPI generation and endpoint explorer wiring.
                does_not_include: A hosted API reference UI.
                start_here_path: Web/ForgeTrust.Runnable.Web.OpenApi/README.md
              - project: Web/ForgeTrust.Runnable.Web.Tailwind/runtimes/ForgeTrust.Runnable.Web.Tailwind.Runtime.osx-arm64.csproj
                classification: support
                order: 30
                note: Restored transitively by `ForgeTrust.Runnable.Web.Tailwind` on matching build hosts. Do not install it directly.
              - project: Web/ForgeTrust.Runnable.Web.RazorDocs/ForgeTrust.Runnable.Web.RazorDocs.csproj
                classification: proof_host
                order: 40
                note: Reusable docs package for hosting harvested repository docs.
                start_here_path: Web/ForgeTrust.Runnable.Web.RazorDocs/README.md
              - project: Web/ForgeTrust.Runnable.Web.RazorWire.Cli/ForgeTrust.Runnable.Web.RazorWire.Cli.csproj
                classification: excluded
                order: 50
                note: Held out of the direct-install chooser until issue #171 lands real tool packaging.
            """);
        await WriteFileAsync("Web/ForgeTrust.Runnable.Web/ForgeTrust.Runnable.Web.csproj", "<Project />");
        await WriteFileAsync("Web/ForgeTrust.Runnable.Web/README.md", "# Web");
        await WriteFileAsync("Web/ForgeTrust.Runnable.Web.OpenApi/ForgeTrust.Runnable.Web.OpenApi.csproj", "<Project />");
        await WriteFileAsync("Web/ForgeTrust.Runnable.Web.OpenApi/README.md", "# OpenApi");
        await WriteFileAsync(
            "Web/ForgeTrust.Runnable.Web.Tailwind/runtimes/ForgeTrust.Runnable.Web.Tailwind.Runtime.osx-arm64.csproj",
            "<Project />");
        await WriteFileAsync("Web/ForgeTrust.Runnable.Web.RazorDocs/ForgeTrust.Runnable.Web.RazorDocs.csproj", "<Project />");
        await WriteFileAsync("Web/ForgeTrust.Runnable.Web.RazorDocs/README.md", "# RazorDocs");
        await WriteFileAsync("Web/ForgeTrust.Runnable.Web.RazorWire.Cli/ForgeTrust.Runnable.Web.RazorWire.Cli.csproj", "<Project />");
        await WriteFileAsync("examples/web-app/README.md", "# Example");
        await WriteFileAsync("releases/README.md", "# Releases");
        await WriteFileAsync("releases/upgrade-policy.md", "# Policy");
        await WriteFileAsync("CHANGELOG.md", "# Changelog");
        if (includeUnreleased)
        {
            await WriteFileAsync("releases/unreleased.md", "# Unreleased");
        }
    }

    private async Task WriteFileAsync(string relativePath, string content)
    {
        var fullPath = Path.Combine(_repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8);
    }

    private static PackageProjectMetadata CreateMetadata(
        string projectPath,
        string packageId,
        string outputType = "Library",
        string targetFramework = "net10.0")
    {
        return new PackageProjectMetadata(projectPath, packageId, targetFramework, true, outputType, []);
    }

    private sealed class FakeMetadataProvider : IProjectMetadataProvider
    {
        private readonly IReadOnlyDictionary<string, PackageProjectMetadata> _metadataByProject;

        public FakeMetadataProvider(IReadOnlyDictionary<string, PackageProjectMetadata> metadataByProject)
        {
            _metadataByProject = metadataByProject;
        }

        public Task<PackageProjectMetadata> GetMetadataAsync(
            string repositoryRoot,
            string projectPath,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_metadataByProject[projectPath]);
        }
    }
}
