using System.Diagnostics;
using System.Text;
using ForgeTrust.AppSurface.Cli;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit.Abstractions;

namespace ForgeTrust.AppSurface.Cli.Tests;

/// <summary>
/// Verifies the test-only #781 structural classifier against immutable patch evidence.
/// </summary>
public sealed class StructuralLineClassifierTests
{
    private static readonly IReadOnlyList<MetadataReference> PlatformReferences =
        ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")
            ?? throw new InvalidOperationException("The trusted platform assembly list is unavailable."))
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .Select(path => MetadataReference.CreateFromFile(path))
        .ToArray();

    private readonly ITestOutputHelper output;

    /// <summary>
    /// Initializes the test output sink used to record the reproducible benchmark observations.
    /// </summary>
    public StructuralLineClassifierTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task Classify_RealPatchEvidence_AcceptsAutoProperty_AndDoesNotMutateCoverageGateArtifacts()
    {
        const string source = """
            namespace Fixture;

            public sealed class Example
            {
                public int Value { get; set; }
            }
            """;
        var propertyLine = LineOf(source, "public int Value");
        using var fixture = await PatchEvidenceFixture.CreateAsync(source, [propertyLine]);
        var context = CreateContext(source);

        var gateRequest = new CoverageGateRequest(
            fixture.CoveragePath,
            fixture.RootPath,
            0,
            0,
            false,
            null,
            fixture.PatchRequest);
        var before = await CoverageGateEvaluator.EvaluateAsync(gateRequest, CancellationToken.None);
        await CoverageGateReportWriter.WriteAsync(before, gateRequest, CancellationToken.None);
        var beforeArtifacts = SnapshotFiles(fixture.RootPath);

        var audit = new StructuralLineClassifier().Classify(fixture.Analysis, context.Compilation, context.Manifest);

        var after = await CoverageGateEvaluator.EvaluateAsync(gateRequest, CancellationToken.None);
        await CoverageGateReportWriter.WriteAsync(after, gateRequest, CancellationToken.None);
        var afterArtifacts = SnapshotFiles(fixture.RootPath);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(StructuralLineDisposition.Accepted, entry.Disposition);
        Assert.Equal(StructuralLineClassificationPolicy.AcceptanceReason, entry.ReasonCode);
        Assert.Equal("src/Fixture.cs", entry.Path);
        Assert.Equal(propertyLine, entry.Line);
        Assert.True(entry.IsMeasured);
        Assert.False(entry.LineCovered);
        Assert.Equal(0, entry.CoveredConditions);
        Assert.Equal(1, entry.ValidConditions);
        Assert.Equal(StructuralLineClassificationPolicy.Identifier, entry.PolicyIdentifier);
        Assert.Equal(StructuralLineClassificationPolicy.Version, entry.PolicyVersion);
        Assert.NotNull(before.PatchAnalysis);
        Assert.NotNull(after.PatchAnalysis);
        Assert.Equal(before.PatchAnalysis.SourceReport, after.PatchAnalysis.SourceReport);
        Assert.Equal(before.PatchAnalysis.LineMode, after.PatchAnalysis.LineMode);
        Assert.Equal(before.PatchAnalysis.Lines, after.PatchAnalysis.Lines);
        Assert.Equal(before.PatchAnalysis.Metrics, after.PatchAnalysis.Metrics);
        Assert.Equal(before.PatchLineCoverage, after.PatchLineCoverage);
        Assert.Equal(before.PatchBranchCoverage, after.PatchBranchCoverage);
        Assert.Equal(beforeArtifacts, afterArtifacts);
        Assert.DoesNotContain(afterArtifacts.Keys, path => path.Contains("structural", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("public int Value { get; set; }", true, "structural-auto-property")]
    [InlineData("public int Value { get; init; }", true, "structural-auto-property")]
    [InlineData("[System.Obsolete] public int Value { get; set; }", false, "property-attributes")]
    [InlineData("public int Value { get; set; } = 7;", false, "property-initializer")]
    [InlineData("public int Value => 7;", false, "accessor-expression-body")]
    [InlineData("public static int Value { get; set; }", false, "property-modifiers")]
    [InlineData("public int Value { private get; set; }", false, "accessor-modifiers")]
    [InlineData("public int Value { get { return 7; } set; }", false, "accessor-body")]
    [InlineData("public int Value { get => 7; set; }", false, "accessor-expression-body")]
    [InlineData("public int Value { get; }", false, "unsupported-property-shape")]
    public void Classify_RecognizesOnlySemicolonOnlyAutoProperties(
        string declaration,
        bool expectedAccepted,
        string expectedReason)
    {
        var source = $$"""
            namespace Fixture;
            public sealed class Example
            {
                {{declaration}}
            }
            """;
        var context = CreateContext(source);

        var entry = ClassifySingle(context, LineOf(source, declaration));

        Assert.Equal(
            expectedAccepted ? StructuralLineDisposition.Accepted : StructuralLineDisposition.Rejected,
            entry.Disposition);
        Assert.Equal(expectedReason, entry.ReasonCode);
    }

    [Fact]
    public void Classify_RejectsBraceOnlyChangedLine()
    {
        const string source = """
            namespace Fixture;
            public sealed class Example
            {
                public int Value
                {
                    get;
                    set;
                }
            }
            """;
        var context = CreateContext(source);

        var entry = ClassifySingle(context, LineOf(source, "public int Value") + 1);

        Assert.Equal(StructuralLineDisposition.Rejected, entry.Disposition);
        Assert.Equal("location-unmatched", entry.ReasonCode);
    }

    [Theory]
    [InlineData("/// <summary>documentation only</summary>")]
    [InlineData("    // implementation comment only")]
    [InlineData("")]
    public void Classify_RejectsNonPropertySourceLines(string changedText)
    {
        var source = $$"""
            namespace Fixture;
            public sealed class Example
            {
            {{changedText}}
                public int Value { get; set; }
            }
            """;
        var context = CreateContext(source);

        var entry = ClassifySingle(context, LineOf(source, changedText));

        Assert.Equal(StructuralLineDisposition.Rejected, entry.Disposition);
        Assert.Equal("location-unmatched", entry.ReasonCode);
    }

    [Fact]
    public void Classify_PrioritizesNotCSharpBeforeMeasurement()
    {
        const string source = "namespace Fixture; public sealed class Example { public int Value { get; set; } }";
        var context = CreateContext(source);
        var analysis = CreateAnalysis(
            new PatchCoverageLine("docs/notes.md", 1, false, null, null, null),
            new PatchCoverageLine("src/Fixture.cs", 1, false, null, null, null));

        var entries = new StructuralLineClassifier().Classify(analysis, context.Compilation, context.Manifest).Entries;

        Assert.Equal("not-csharp", entries[0].ReasonCode);
        Assert.Equal("not-measured", entries[1].ReasonCode);
    }

    [Fact]
    public void Classify_FailsClosedForSourceIdentityAndProvenanceGaps()
    {
        const string source = "namespace Fixture; public sealed class Example { public int Value { get; set; } }";
        var context = CreateContext(source);
        var line = new PatchCoverageLine("src/Fixture.cs", 1, true, false, 0, 1);

        var missing = ClassifySingle(context, 1, new StructuralSourceManifest([]));
        var ambiguous = ClassifySingle(context, 1, new StructuralSourceManifest([context.Document, context.Document]));
        var fingerprint = ClassifySingle(
            context,
            1,
            new StructuralSourceManifest([context.Document with { Fingerprint = "not-the-source-fingerprint" }]));
        var generated = ClassifySingle(
            context,
            1,
            new StructuralSourceManifest([context.Document with { IsGenerated = true }]));
        var synthesized = ClassifySingle(
            context,
            1,
            new StructuralSourceManifest([context.Document with { IsSynthesized = true }]));
        var otherPathTree = CSharpSyntaxTree.ParseText(
            source,
            context.Document.ParseOptions,
            "src/Other.cs");
        var pathMismatchDocument = context.Document with { SyntaxTree = otherPathTree };
        var pathMismatchCompilation = CSharpCompilation.Create(
            "Fixture",
            [otherPathTree],
            PlatformReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var pathMismatch = ClassifySingle(
            context,
            1,
            new StructuralSourceManifest([pathMismatchDocument]),
            pathMismatchCompilation);

        Assert.Equal("source-missing", missing.ReasonCode);
        Assert.Equal("source-ambiguous", ambiguous.ReasonCode);
        Assert.Equal("source-fingerprint-mismatch", fingerprint.ReasonCode);
        Assert.Equal("source-generated", generated.ReasonCode);
        Assert.Equal("source-generated", synthesized.ReasonCode);
        Assert.Equal("source-fingerprint-mismatch", pathMismatch.ReasonCode);
        Assert.All(
            [missing, ambiguous, fingerprint, generated, synthesized, pathMismatch],
            entry => Assert.Equal(line.LineCovered, entry.LineCovered));
    }

    [Fact]
    public void Classify_FailsClosedWhenTheCompilationDoesNotMatchTheFixture()
    {
        const string source = "namespace Fixture; public sealed class Example { public int Value { get; set; } }";
        var context = CreateContext(source);
        var otherTree = CSharpSyntaxTree.ParseText(source, context.Document.ParseOptions, "src/Fixture.cs");
        var wrongCompilation = CSharpCompilation.Create(
            "Fixture",
            [otherTree],
            PlatformReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var alternateOptions = context.Document.ParseOptions.WithPreprocessorSymbols("ALTERNATE");
        var alternateTree = CSharpSyntaxTree.ParseText(source, alternateOptions, "src/Fixture.cs");
        var alternateDocument = StructuralSourceDocument.Create(
            "src/Fixture.cs",
            Encoding.UTF8.GetBytes(source),
            context.Document.ParseOptions,
            syntaxTree: alternateTree);
        var alternateCompilation = CSharpCompilation.Create(
            "Fixture",
            [alternateTree],
            PlatformReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var treeMismatch = ClassifySingle(context, 1, compilation: wrongCompilation);
        var conditionalMismatch = ClassifySingle(
            context,
            1,
            new StructuralSourceManifest([alternateDocument]),
            alternateCompilation);

        Assert.Equal("compilation-mismatch", treeMismatch.ReasonCode);
        Assert.Equal("conditional-compilation-mismatch", conditionalMismatch.ReasonCode);
    }

    [Theory]
    [InlineData("public partial class Example { public int Value { get; set; } }", "partial-type")]
    [InlineData("public sealed class Example { public MissingType Value { get; set; } }", "property-unbound")]
    [InlineData("public sealed class Example { public int Value { get; set; } = Missing; }", "semantic-diagnostic")]
    public void Classify_FailsClosedForSemanticUncertainty(string body, string expectedReason)
    {
        var source = "namespace Fixture; " + body;
        var context = CreateContext(source);

        var entry = ClassifySingle(context, 1);

        Assert.Equal(StructuralLineDisposition.Rejected, entry.Disposition);
        Assert.Equal(expectedReason, entry.ReasonCode);
    }

    [Fact]
    public void Classify_FailsClosedForAnalysisExceptionsWithoutLeakingExceptionText()
    {
        const string source = "namespace Fixture; public sealed class Example { public int Value { get; set; } }";
        var context = CreateContext(source);

        var audit = new StructuralLineClassifier(_ => new InvalidOperationException("fixture secret"))
            .Classify(CreateAnalysis(new PatchCoverageLine("src/Fixture.cs", 1, true, false, 0, 1)), context.Compilation, context.Manifest);

        var entry = Assert.Single(audit.Entries);
        Assert.Equal(StructuralLineDisposition.Rejected, entry.Disposition);
        Assert.Equal("analysis-fault", entry.ReasonCode);
        Assert.Equal("exceptionType=InvalidOperationException", entry.Metadata);
        Assert.DoesNotContain("fixture secret", entry.Metadata, StringComparison.Ordinal);
    }

    [Fact]
    public void Classify_RejectsOverriddenProperties()
    {
        const string source = """
            namespace Fixture;
            public class Base
            {
                public virtual int Value { get; set; }
            }

            public sealed class Derived : Base
            {
                public override int Value { get; set; }
            }
            """;
        var context = CreateContext(source);

        var entry = ClassifySingle(context, LineOf(source, "public override int Value"));

        Assert.Equal(StructuralLineDisposition.Rejected, entry.Disposition);
        Assert.Equal("property-inherited-or-overridden", entry.ReasonCode);
    }

    [Fact]
    public void Classify_OrdersEntriesAndSerializesTheAuditDeterministically()
    {
        const string sourceA = "namespace Fixture; public sealed class A { public int Value { get; set; } }";
        const string sourceB = "namespace Fixture; public sealed class B { public int Value { get; set; } }";
        var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var documentA = StructuralSourceDocument.Create("src/A.cs", Encoding.UTF8.GetBytes(sourceA), options);
        var documentB = StructuralSourceDocument.Create("src/B.cs", Encoding.UTF8.GetBytes(sourceB), options);
        var compilation = CSharpCompilation.Create(
            "Fixture",
            [documentA.SyntaxTree, documentB.SyntaxTree],
            PlatformReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var analysis = CreateAnalysis(
            new PatchCoverageLine("src/B.cs", 1, true, true, null, null),
            new PatchCoverageLine("src/A.cs", 1, true, false, 0, 1),
            new PatchCoverageLine("docs/Readme.md", 4, false, null, null, null));

        var audit = new StructuralLineClassifier().Classify(
            analysis,
            compilation,
            new StructuralSourceManifest([documentB, documentA]));

        Assert.Equal(["docs/Readme.md", "src/A.cs", "src/B.cs"], audit.Entries.Select(entry => entry.Path));
        Assert.Equal(audit.ToDeterministicJson(), audit.ToDeterministicJson());
        Assert.Contains("\"policyIdentifier\": \"appsurface.structural-line\"", audit.ToDeterministicJson(), StringComparison.Ordinal);
        Assert.Contains("\"disposition\": \"accepted\"", audit.ToDeterministicJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void Classify_ReusedFixtures_StaysWithinTheAllocationBudget_AndRecordsTimingDistribution()
    {
        const int candidateCount = 200;
        var source = BuildLargeSource(candidateCount);
        var context = CreateContext(source);
        var analysis = CreateAnalysis(
            Enumerable.Range(1, candidateCount)
                .Select(index => new PatchCoverageLine("src/Fixture.cs", index + 3, true, false, 0, 1))
                .ToArray());
        var classifier = new StructuralLineClassifier();

        _ = classifier.Classify(analysis, context.Compilation, context.Manifest);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var samples = new List<long>();
        var constructionSamples = new List<long>();
        long maxAllocatedBytes = 0;
        for (var iteration = 0; iteration < 25; iteration++)
        {
            var beforeAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            var audit = classifier.Classify(analysis, context.Compilation, context.Manifest);
            stopwatch.Stop();
            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - beforeAllocatedBytes;

            Assert.Equal(candidateCount, audit.Entries.Count);
            samples.Add(stopwatch.ElapsedTicks);
            maxAllocatedBytes = Math.Max(maxAllocatedBytes, allocatedBytes);
        }

        for (var iteration = 0; iteration < 10; iteration++)
        {
            var stopwatch = Stopwatch.StartNew();
            _ = CreateContext(source);
            stopwatch.Stop();
            constructionSamples.Add(stopwatch.ElapsedTicks);
        }

        samples.Sort();
        constructionSamples.Sort();
        var tickToMilliseconds = 1000d / Stopwatch.Frequency;
        output.WriteLine(
            "#781 structural classifier benchmark: candidates={0}; samples=25; minMs={1:F4}; p50Ms={2:F4}; p95Ms={3:F4}; maxMs={4:F4}; maxAllocatedBytes={5}; compilationSamples=10; compilationMinMs={6:F4}; compilationP50Ms={7:F4}; compilationP95Ms={8:F4}",
            candidateCount,
            samples[0] * tickToMilliseconds,
            samples[PercentileIndex(samples.Count, 0.50)] * tickToMilliseconds,
            samples[PercentileIndex(samples.Count, 0.95)] * tickToMilliseconds,
            samples[^1] * tickToMilliseconds,
            maxAllocatedBytes,
            constructionSamples[0] * tickToMilliseconds,
            constructionSamples[PercentileIndex(constructionSamples.Count, 0.50)] * tickToMilliseconds,
            constructionSamples[PercentileIndex(constructionSamples.Count, 0.95)] * tickToMilliseconds);
        Assert.InRange(maxAllocatedBytes, 0, 24 * 1024 * 1024);
    }

    private static ClassificationContext CreateContext(string source)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var document = StructuralSourceDocument.Create(
            "src/Fixture.cs",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(source),
            parseOptions);
        var compilation = CSharpCompilation.Create(
            "Fixture",
            [document.SyntaxTree],
            PlatformReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return new ClassificationContext(document, compilation, new StructuralSourceManifest([document]));
    }

    private static StructuralLineClassificationEntry ClassifySingle(
        ClassificationContext context,
        int line,
        StructuralSourceManifest? manifest = null,
        CSharpCompilation? compilation = null)
    {
        var audit = new StructuralLineClassifier().Classify(
            CreateAnalysis(new PatchCoverageLine("src/Fixture.cs", line, true, false, 0, 1)),
            compilation ?? context.Compilation,
            manifest ?? context.Manifest);
        return Assert.Single(audit.Entries);
    }

    private static PatchCoverageAnalysis CreateAnalysis(params PatchCoverageLine[] lines)
    {
        var sourceReport = new PatchDiffSourceReport(
            PatchDiffSourceKind.GitBase,
            "fixture-base",
            "fixture-base",
            null,
            0,
            "fixture-sha256",
            false,
            false);
        var measuredLines = lines.Count(line => line.IsMeasured);
        var coveredLines = lines.Count(line => line.LineCovered is true);
        var validConditions = lines.Sum(line => line.ValidConditions ?? 0);
        var coveredConditions = lines.Sum(line => line.CoveredConditions ?? 0);
        var metrics = new PatchCoverageMetrics(
            sourceReport,
            new PatchLineCoverageMetric("fixture-base", lines.Length, measuredLines, coveredLines, 100),
            new PatchBranchCoverageMetric("fixture-base", lines.Length, validConditions, coveredConditions, 100));
        return new PatchCoverageAnalysis(sourceReport, PatchLineMode.Measurable, lines, metrics);
    }

    private static IReadOnlyDictionary<string, string> SnapshotFiles(string rootPath) =>
        Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToDictionary(
                path => Path.GetRelativePath(rootPath, path),
                File.ReadAllText,
                StringComparer.Ordinal);

    private static int LineOf(string source, string text)
    {
        var index = source.IndexOf(text, StringComparison.Ordinal);
        Assert.True(index >= 0, $"Could not find '{text}' in the fixture source.");
        return source[..index].Count(character => character == '\n') + 1;
    }

    private static int PercentileIndex(int sampleCount, double percentile) =>
        Math.Clamp((int)Math.Ceiling(sampleCount * percentile) - 1, 0, sampleCount - 1);

    private static string BuildLargeSource(int candidateCount)
    {
        var builder = new StringBuilder("namespace Fixture;\npublic sealed class Example\n{\n");
        for (var index = 0; index < candidateCount; index++)
        {
            builder.Append("    public int Value").Append(index).Append(" { get; set; }\n");
        }

        return builder.Append('}').ToString();
    }

    private sealed record ClassificationContext(
        StructuralSourceDocument Document,
        CSharpCompilation Compilation,
        StructuralSourceManifest Manifest);

    private sealed class PatchEvidenceFixture : IDisposable
    {
        private PatchEvidenceFixture(
            string rootPath,
            string coveragePath,
            CoveragePatchRequest patchRequest,
            PatchCoverageAnalysis analysis)
        {
            RootPath = rootPath;
            CoveragePath = coveragePath;
            PatchRequest = patchRequest;
            Analysis = analysis;
        }

        public string RootPath { get; }

        public string CoveragePath { get; }

        public CoveragePatchRequest PatchRequest { get; }

        public PatchCoverageAnalysis Analysis { get; }

        public static async Task<PatchEvidenceFixture> CreateAsync(string source, IReadOnlyList<int> changedLines)
        {
            var rootPath = Path.Join(Path.GetTempPath(), "appsurface-structural-classifier-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Join(rootPath, "src"));
            await File.WriteAllTextAsync(Path.Join(rootPath, "src", "Fixture.cs"), source, new UTF8Encoding(false));
            var coveragePath = Path.Join(rootPath, "coverage.cobertura.xml");
            await File.WriteAllTextAsync(coveragePath, BuildCoverage(changedLines), new UTF8Encoding(false));
            var diffText = BuildDiff(changedLines);
            var patchRequest = new CoveragePatchRequest(
                rootPath,
                "fixture-base",
                0,
                _ => Task.FromResult(diffText));
            var analysis = await PatchCoverageEvaluator.AnalyzeAsync(
                coveragePath,
                patchRequest,
                CancellationToken.None);
            return new PatchEvidenceFixture(rootPath, coveragePath, patchRequest, analysis);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }

        private static string BuildCoverage(IEnumerable<int> changedLines)
        {
            var lines = changedLines.Order().ToArray();
            var entries = string.Join(
                Environment.NewLine,
                lines.Select(line => $"<line number=\"{line}\" hits=\"0\" branch=\"true\" condition-coverage=\"0% (0/1)\" />"));
            return $"""
                <coverage lines-covered="0" lines-valid="{lines.Length}" branches-covered="0" branches-valid="{lines.Length}">
                  <packages>
                    <package name="Fixture">
                      <classes>
                        <class name="Fixture.Example" filename="src/Fixture.cs">
                          <lines>
                            {entries}
                          </lines>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """;
        }

        private static string BuildDiff(IEnumerable<int> changedLines)
        {
            var builder = new StringBuilder(
                "diff --git a/src/Fixture.cs b/src/Fixture.cs\nindex 0000000..1111111 100644\n--- a/src/Fixture.cs\n+++ b/src/Fixture.cs\n");
            foreach (var line in changedLines.Order())
            {
                builder.Append("@@ -0,0 +").Append(line).Append(",1 @@\n+fixture evidence\n");
            }

            return builder.ToString();
        }
    }
}
