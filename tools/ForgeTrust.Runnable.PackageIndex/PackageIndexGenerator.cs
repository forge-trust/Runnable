using System.Diagnostics;
using System.Text;
using System.Text.Json;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ForgeTrust.Runnable.PackageIndex;

internal sealed class PackageIndexGenerator
{
    private const string WebPackageId = "ForgeTrust.Runnable.Web";
    private const string RazorWireCliPackageId = "ForgeTrust.Runnable.Web.RazorWire.Cli";
    private const string ReleaseHubPath = "releases/README.md";
    private const string UnreleasedPath = "releases/unreleased.md";
    private const string ChangelogPath = "CHANGELOG.md";
    private const string UpgradePolicyPath = "releases/upgrade-policy.md";
    private const string WebExamplePath = "examples/web-app/README.md";

    private readonly PackageProjectScanner _scanner;
    private readonly IProjectMetadataProvider _metadataProvider;
    private readonly PackageManifestLoader _manifestLoader;

    internal PackageIndexGenerator(
        PackageProjectScanner scanner,
        IProjectMetadataProvider metadataProvider,
        PackageManifestLoader manifestLoader)
    {
        _scanner = scanner;
        _metadataProvider = metadataProvider;
        _manifestLoader = manifestLoader;
    }

    internal async Task GenerateToFileAsync(PackageIndexRequest request, CancellationToken cancellationToken = default)
    {
        var markdown = await GenerateAsync(request, cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(request.OutputPath)!);
        await File.WriteAllTextAsync(request.OutputPath, markdown, cancellationToken);
    }

    internal async Task<string> GenerateAsync(PackageIndexRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var manifest = await _manifestLoader.LoadAsync(request.ManifestPath, cancellationToken);
        var candidateProjects = _scanner.DiscoverProjects(request.RepositoryRoot);
        var metadata = await LoadMetadataAsync(request.RepositoryRoot, candidateProjects, cancellationToken);
        var entries = ResolveEntries(request.RepositoryRoot, manifest, candidateProjects, metadata);
        return RenderMarkdown(request.RepositoryRoot, entries);
    }

    internal async Task VerifyAsync(PackageIndexRequest request, CancellationToken cancellationToken = default)
    {
        var expected = await GenerateAsync(request, cancellationToken);
        if (!File.Exists(request.OutputPath))
        {
            throw new PackageIndexException(
                $"Missing generated file '{Path.GetRelativePath(request.RepositoryRoot, request.OutputPath)}'. Run the package index generator.");
        }

        var current = await File.ReadAllTextAsync(request.OutputPath, cancellationToken);
        if (!string.Equals(current, expected, StringComparison.Ordinal))
        {
            throw new PackageIndexException(
                $"Generated file '{Path.GetRelativePath(request.RepositoryRoot, request.OutputPath)}' is stale. Run the package index generator.");
        }
    }

    private static void ValidateRequest(PackageIndexRequest request)
    {
        if (!Directory.Exists(request.RepositoryRoot))
        {
            throw new PackageIndexException($"Repository root '{request.RepositoryRoot}' does not exist.");
        }

        if (!File.Exists(request.ManifestPath))
        {
            throw new PackageIndexException(
                $"Manifest '{Path.GetRelativePath(request.RepositoryRoot, request.ManifestPath)}' does not exist.");
        }

        var sidecarPath = Path.Combine(Path.GetDirectoryName(request.OutputPath)!, "README.md.yml");
        if (!File.Exists(sidecarPath))
        {
            throw new PackageIndexException(
                $"Expected paired sidecar '{Path.GetRelativePath(request.RepositoryRoot, sidecarPath)}' to exist beside the generated chooser.");
        }
    }

    private async Task<IReadOnlyDictionary<string, PackageProjectMetadata>> LoadMetadataAsync(
        string repositoryRoot,
        IReadOnlyList<string> candidateProjects,
        CancellationToken cancellationToken)
    {
        var metadataByPath = new Dictionary<string, PackageProjectMetadata>(StringComparer.OrdinalIgnoreCase);
        foreach (var projectPath in candidateProjects)
        {
            var metadata = await _metadataProvider.GetMetadataAsync(repositoryRoot, projectPath, cancellationToken);
            metadataByPath.Add(projectPath, metadata);
        }

        return metadataByPath;
    }

    private static IReadOnlyList<ResolvedPackageEntry> ResolveEntries(
        string repositoryRoot,
        PackageManifest manifest,
        IReadOnlyList<string> candidateProjects,
        IReadOnlyDictionary<string, PackageProjectMetadata> metadataByPath)
    {
        var manifestByProject = manifest.Packages
            .GroupBy(entry => entry.Project, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        foreach (var duplicate in manifestByProject.Where(group => group.Value.Length > 1))
        {
            throw new PackageIndexException($"Manifest declares '{duplicate.Key}' more than once.");
        }

        foreach (var projectPath in candidateProjects)
        {
            if (!manifestByProject.ContainsKey(projectPath))
            {
                throw new PackageIndexException($"Manifest is missing a classification for '{projectPath}'.");
            }
        }

        foreach (var manifestPath in manifestByProject.Keys)
        {
            if (!metadataByPath.ContainsKey(manifestPath))
            {
                throw new PackageIndexException($"Manifest references '{manifestPath}', but the project was not discovered.");
            }
        }

        var knownPackageIds = metadataByPath.Values
            .Select(entry => entry.PackageId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var resolvedEntries = new List<ResolvedPackageEntry>(manifest.Packages.Count);
        foreach (var manifestEntry in manifest.Packages.OrderBy(entry => entry.Order))
        {
            var metadata = metadataByPath[manifestEntry.Project];
            ValidateManifestEntry(repositoryRoot, manifestEntry, metadata, knownPackageIds);
            resolvedEntries.Add(new ResolvedPackageEntry(manifestEntry, metadata));
        }

        if (!resolvedEntries.Any(entry => string.Equals(entry.Metadata.PackageId, WebPackageId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new PackageIndexException($"Manifest must include the '{WebPackageId}' package.");
        }

        return resolvedEntries;
    }

    private static void ValidateManifestEntry(
        string repositoryRoot,
        PackageManifestEntry entry,
        PackageProjectMetadata metadata,
        IReadOnlySet<string> knownPackageIds)
    {
        if (entry.Classification == PackageClassification.Public)
        {
            RequireValue(entry.Project, nameof(entry.UseWhen), entry.UseWhen);
            RequireValue(entry.Project, nameof(entry.Includes), entry.Includes);
            RequireValue(entry.Project, nameof(entry.DoesNotInclude), entry.DoesNotInclude);
            RequireValue(entry.Project, nameof(entry.StartHerePath), entry.StartHerePath);
        }

        if (!string.IsNullOrWhiteSpace(entry.StartHerePath))
        {
            var resolvedPath = Path.Combine(repositoryRoot, entry.StartHerePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(resolvedPath))
            {
                throw new PackageIndexException(
                    $"Manifest entry '{entry.Project}' points at missing documentation '{entry.StartHerePath}'.");
            }
        }

        foreach (var dependency in entry.DependsOn)
        {
            if (!knownPackageIds.Contains(dependency))
            {
                throw new PackageIndexException(
                    $"Manifest entry '{entry.Project}' depends on unknown package id '{dependency}'.");
            }
        }

        if (string.Equals(metadata.PackageId, RazorWireCliPackageId, StringComparison.OrdinalIgnoreCase)
            && entry.Classification != PackageClassification.Excluded)
        {
            throw new PackageIndexException(
                $"{RazorWireCliPackageId} must stay excluded from direct-install guidance until issue #171 lands.");
        }
    }

    private static void RequireValue(string projectPath, string propertyName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new PackageIndexException($"Manifest entry '{projectPath}' must define '{propertyName}'.");
        }
    }

    private static string RenderMarkdown(string repositoryRoot, IReadOnlyList<ResolvedPackageEntry> entries)
    {
        var publicEntries = entries.Where(entry => entry.Manifest.Classification == PackageClassification.Public).ToArray();
        var supportEntries = entries.Where(entry => entry.Manifest.Classification == PackageClassification.Support).ToArray();
        var proofHostEntries = entries.Where(entry => entry.Manifest.Classification == PackageClassification.ProofHost).ToArray();
        var excludedEntries = entries.Where(entry => entry.Manifest.Classification == PackageClassification.Excluded).ToArray();
        var webEntry = publicEntries.Single(entry => string.Equals(entry.Metadata.PackageId, WebPackageId, StringComparison.OrdinalIgnoreCase));
        var publicTargetFrameworks = publicEntries
            .Select(entry => entry.Metadata.TargetFramework)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var targetFrameworkSummary = publicTargetFrameworks.Length == 1
            ? $"All direct-install packages currently target `{publicTargetFrameworks[0]}`."
            : $"Direct-install packages currently target {string.Join(", ", publicTargetFrameworks.Select(value => $"`{value}`"))}.";

        var builder = new StringBuilder();
        builder.AppendLine("# Runnable v0.1 package chooser");
        builder.AppendLine();
        builder.AppendLine("> Generated from `packages/package-index.yml` and evaluated project metadata. Do not edit this file by hand.");
        builder.AppendLine();
        builder.AppendLine("Runnable v0.1 is a coordinated .NET 10 package family. Start with the package that matches the app you're building, then add optional modules only when your app needs them.");
        builder.AppendLine();
        builder.AppendLine($"{targetFrameworkSummary} In .NET 10, `dotnet package add` and `dotnet add package` are equivalent. This chooser uses `dotnet package add`, while `dotnet add package` remains the familiar cross-version form on older SDKs.");
        builder.AppendLine();
        builder.AppendLine("## Web app");
        builder.AppendLine();
        builder.AppendLine(webEntry.Manifest.UseWhen!);
        builder.AppendLine();
        builder.AppendLine("```bash");
        builder.AppendLine(webEntry.Metadata.InstallCommand);
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine($"What you get: {webEntry.Manifest.Includes}");
        builder.AppendLine();
        builder.AppendLine($"Not included: {webEntry.Manifest.DoesNotInclude}");
        builder.AppendLine();
        builder.AppendLine($"Read next: {FormatMarkdownLink("examples/web-app/README.md", GetRelativeDocPath(WebExamplePath))}");
        builder.AppendLine();
        builder.AppendLine("Release and readiness:");
        builder.AppendLine($"- {FormatMarkdownLink("Release hub", GetRelativeDocPath(ReleaseHubPath))} keeps the public release story, adoption risk, and policy links in one place.");
        if (File.Exists(Path.Combine(repositoryRoot, UnreleasedPath.Replace('/', Path.DirectorySeparatorChar))))
        {
            builder.AppendLine($"- {FormatMarkdownLink("Unreleased proof artifact", GetRelativeDocPath(UnreleasedPath))} shows what is queued for the next coordinated version.");
        }
        else
        {
            builder.AppendLine("- Unreleased proof artifact: Not published yet. This row stays visible so the chooser does not quietly hide missing release-state evidence.");
        }

        builder.AppendLine($"- {FormatMarkdownLink("CHANGELOG.md", GetRelativeDocPath(ChangelogPath))} is the compact ledger for tagged and in-flight package changes.");
        builder.AppendLine($"- {FormatMarkdownLink("Pre-1.0 upgrade policy", GetRelativeDocPath(UpgradePolicyPath))} explains the current stability contract before `v1.0.0`.");
        builder.AppendLine();
        builder.AppendLine("## Also building...");
        builder.AppendLine();
        foreach (var recipeEntry in publicEntries.Where(entry => !string.IsNullOrWhiteSpace(entry.Manifest.RecipeSummary)
                                                                 && !string.Equals(entry.Metadata.PackageId, WebPackageId, StringComparison.OrdinalIgnoreCase)))
        {
            builder.AppendLine($"- {recipeEntry.Manifest.RecipeSummary}");
        }

        builder.AppendLine();
        builder.AppendLine("## Package matrix");
        builder.AppendLine();
        builder.AppendLine("Swipe to compare package details on narrow screens.");
        builder.AppendLine();
        builder.AppendLine("| Package | Use when | Install | Includes | Does not include | Start here |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- |");
        foreach (var entry in publicEntries)
        {
            builder.Append("| ");
            builder.Append(EscapeTableCell($"`{entry.Metadata.PackageId}`"));
            builder.Append(" | ");
            builder.Append(EscapeTableCell(entry.Manifest.UseWhen!));
            builder.Append(" | ");
            builder.Append(EscapeTableCell($"`{entry.Metadata.InstallCommand}`"));
            builder.Append(" | ");
            builder.Append(EscapeTableCell(entry.Manifest.Includes!));
            builder.Append(" | ");
            builder.Append(EscapeTableCell(entry.Manifest.DoesNotInclude!));
            builder.Append(" | ");
            builder.Append(EscapeTableCell(FormatMarkdownLink(entry.Manifest.StartHereLabel ?? "Package README", GetRelativeDocPath(entry.Manifest.StartHerePath!))));
            builder.AppendLine(" |");
        }

        builder.AppendLine();
        builder.AppendLine("## Support and proof-host surfaces");
        builder.AppendLine();

        if (supportEntries.Length > 0)
        {
            builder.AppendLine("### Support and runtime packages");
            builder.AppendLine();
            foreach (var entry in supportEntries)
            {
                builder.Append("- ");
                builder.Append($"`{entry.Metadata.PackageId}`");
                builder.Append(": ");
                builder.Append(entry.Manifest.Note);
                if (!string.IsNullOrWhiteSpace(entry.Manifest.StartHerePath))
                {
                    builder.Append(" Start here: ");
                    builder.Append(FormatMarkdownLink(entry.Manifest.StartHereLabel ?? "README", GetRelativeDocPath(entry.Manifest.StartHerePath!)));
                }

                builder.AppendLine();
            }

            builder.AppendLine();
        }

        if (proofHostEntries.Length > 0)
        {
            builder.AppendLine("### Docs and proof hosts");
            builder.AppendLine();
            foreach (var entry in proofHostEntries)
            {
                builder.Append("- ");
                builder.Append($"`{entry.Metadata.PackageId}`");
                builder.Append(": ");
                builder.Append(entry.Manifest.Note);
                if (!string.IsNullOrWhiteSpace(entry.Manifest.StartHerePath))
                {
                    builder.Append(" Start here: ");
                    builder.Append(FormatMarkdownLink(entry.Manifest.StartHereLabel ?? "README", GetRelativeDocPath(entry.Manifest.StartHerePath!)));
                }

                builder.AppendLine();
            }

            builder.AppendLine();
        }

        if (excludedEntries.Length > 0)
        {
            builder.AppendLine("### Not in the direct-install matrix");
            builder.AppendLine();
            foreach (var entry in excludedEntries)
            {
                builder.Append("- ");
                builder.Append($"`{entry.Metadata.PackageId}`");
                builder.Append(": ");
                builder.Append(entry.Manifest.Note);
                builder.AppendLine();
            }

            builder.AppendLine();
        }

        builder.AppendLine("## Maintainer notes");
        builder.AppendLine();
        builder.AppendLine($"- Edit `packages/package-index.yml` when the public package story changes.");
        builder.AppendLine($"- Run `dotnet run --project tools/ForgeTrust.Runnable.PackageIndex/ForgeTrust.Runnable.PackageIndex.csproj -- generate` after changing package classifications or package READMEs.");
        builder.AppendLine("- Keep `packages/README.md.yml` hand-authored so RazorDocs metadata, trust-bar copy, and section placement stay intentional.");

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static string GetRelativeDocPath(string repositoryRelativePath)
    {
        var relativePath = Path.GetRelativePath("packages", repositoryRelativePath)
            .Replace('\\', '/');
        return relativePath;
    }

    private static string EscapeTableCell(string value)
    {
        return value
            .Replace("\r\n", "<br />", StringComparison.Ordinal)
            .Replace("\n", "<br />", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static string FormatMarkdownLink(string label, string relativePath)
    {
        return $"[{label}]({relativePath})";
    }
}

internal sealed record PackageIndexRequest(string RepositoryRoot, string ManifestPath, string OutputPath);

internal sealed record ResolvedPackageEntry(PackageManifestEntry Manifest, PackageProjectMetadata Metadata);

internal sealed class PackageIndexException : Exception
{
    internal PackageIndexException(string message)
        : base(message)
    {
    }
}

internal interface IProjectMetadataProvider
{
    Task<PackageProjectMetadata> GetMetadataAsync(
        string repositoryRoot,
        string projectPath,
        CancellationToken cancellationToken);
}

internal sealed record PackageProjectMetadata(
    string ProjectPath,
    string PackageId,
    string TargetFramework,
    bool IsPackable,
    string OutputType,
    IReadOnlyList<string> ProjectReferences)
{
    internal string InstallCommand => $"dotnet package add {PackageId}";
}

internal sealed class PackageProjectScanner
{
    internal IReadOnlyList<string> DiscoverProjects(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        return Directory.EnumerateFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'))
            .Where(IsCandidateProject)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static bool IsCandidateProject(string relativePath)
    {
        var normalizedPath = "/" + relativePath.Replace('\\', '/').Trim('/').ToLowerInvariant();
        var projectName = Path.GetFileNameWithoutExtension(relativePath).ToLowerInvariant();

        if (normalizedPath.Contains("/.git/", StringComparison.Ordinal)
            || normalizedPath.Contains("/.gstack/", StringComparison.Ordinal)
            || normalizedPath.Contains("/.agent/", StringComparison.Ordinal)
            || normalizedPath.Contains("/.claude/", StringComparison.Ordinal)
            || normalizedPath.Contains("/.codex/", StringComparison.Ordinal)
            || normalizedPath.Contains("/bin/", StringComparison.Ordinal)
            || normalizedPath.Contains("/obj/", StringComparison.Ordinal)
            || normalizedPath.Contains("/node_modules/", StringComparison.Ordinal)
            || normalizedPath.Contains("/tools/", StringComparison.Ordinal))
        {
            return false;
        }

        if (normalizedPath.Contains("/examples/", StringComparison.Ordinal)
            || normalizedPath.Contains("/benchmarks/", StringComparison.Ordinal)
            || projectName.Contains("benchmarks", StringComparison.Ordinal))
        {
            return false;
        }

        if (normalizedPath.Contains("/tests/", StringComparison.Ordinal)
            || normalizedPath.Contains(".tests", StringComparison.Ordinal)
            || normalizedPath.Contains("integrationtests", StringComparison.Ordinal)
            || projectName.Contains("tests", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }
}

internal sealed class DotNetProjectMetadataProvider : IProjectMetadataProvider
{
    internal const string TargetFrameworksPropertyName = "TargetFrameworks";

    public async Task<PackageProjectMetadata> GetMetadataAsync(
        string repositoryRoot,
        string projectPath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("msbuild");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("-getProperty:PackageId,TargetFramework,TargetFrameworks,IsPackable,OutputType");
        startInfo.ArgumentList.Add("-getItem:ProjectReference");

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new PackageIndexException(
                $"Failed to evaluate '{projectPath}' with dotnet msbuild.{Environment.NewLine}{standardError}");
        }

        try
        {
            using var document = JsonDocument.Parse(standardOutput);
            var properties = document.RootElement.GetProperty("Properties");

            var packageId = properties.GetProperty("PackageId").GetString();
            var targetFramework = properties.GetProperty("TargetFramework").GetString();
            var targetFrameworks = properties.TryGetProperty(TargetFrameworksPropertyName, out var tfmsElement)
                ? tfmsElement.GetString()
                : null;
            var isPackable = properties.GetProperty("IsPackable").GetString();
            var outputType = properties.GetProperty("OutputType").GetString();
            var projectReferences = ReadProjectReferences(document.RootElement);

            var resolvedTargetFramework = !string.IsNullOrWhiteSpace(targetFramework)
                ? targetFramework
                : targetFrameworks;

            if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(resolvedTargetFramework) || string.IsNullOrWhiteSpace(outputType))
            {
                throw new PackageIndexException($"dotnet msbuild returned incomplete metadata for '{projectPath}'.");
            }

            return new PackageProjectMetadata(
                projectPath,
                packageId,
                resolvedTargetFramework,
                bool.TryParse(isPackable, out var parsedIsPackable) && parsedIsPackable,
                outputType,
                projectReferences);
        }
        catch (JsonException ex)
        {
            throw new PackageIndexException(
                $"dotnet msbuild returned malformed JSON for '{projectPath}': {ex.Message}");
        }
    }

    private static IReadOnlyList<string> ReadProjectReferences(JsonElement root)
    {
        if (!root.TryGetProperty("Items", out var itemsElement)
            || !itemsElement.TryGetProperty("ProjectReference", out var projectReferencesElement)
            || projectReferencesElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return projectReferencesElement.EnumerateArray()
            .Select(element => element.TryGetProperty("FullPath", out var fullPathElement)
                ? fullPathElement.GetString()
                : null)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToArray();
    }
}

internal sealed class PackageManifestLoader
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithEnumNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    internal async Task<PackageManifest> LoadAsync(string manifestPath, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(manifestPath, cancellationToken);
        PackageManifest? manifest;
        try
        {
            manifest = _deserializer.Deserialize<PackageManifest>(content);
        }
        catch (YamlException ex)
        {
            throw new PackageIndexException($"Manifest '{manifestPath}' could not be parsed: {ex.Message}");
        }

        if (manifest is null || manifest.Packages.Count == 0)
        {
            throw new PackageIndexException($"Manifest '{manifestPath}' does not define any packages.");
        }

        return manifest;
    }
}

internal sealed class PackageManifest
{
    public List<PackageManifestEntry> Packages { get; init; } = [];
}

internal sealed class PackageManifestEntry
{
    public string Project { get; init; } = string.Empty;

    public PackageClassification Classification { get; init; }

    public int Order { get; init; }

    public string? UseWhen { get; init; }

    public string? Includes { get; init; }

    public string? DoesNotInclude { get; init; }

    public string? StartHerePath { get; init; }

    public string? StartHereLabel { get; init; }

    public string? RecipeSummary { get; init; }

    public string? Note { get; init; }

    public List<string> DependsOn { get; init; } = [];
}

internal enum PackageClassification
{
    Public,
    Support,
    ProofHost,
    Excluded
}
