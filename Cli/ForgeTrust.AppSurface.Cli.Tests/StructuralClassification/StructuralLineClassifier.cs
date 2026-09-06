using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ForgeTrust.AppSurface.Cli;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ForgeTrust.AppSurface.Cli.Tests;

/// <summary>
/// Identifies the sole test-only structural category considered by the #781 spike.
/// </summary>
internal static class StructuralLineClassificationPolicy
{
    /// <summary>
    /// Gets the stable identifier emitted by every audit entry.
    /// </summary>
    internal const string Identifier = "appsurface.structural-line";

    /// <summary>
    /// Gets the first experimental version of the policy.
    /// </summary>
    internal const string Version = "v0";

    /// <summary>
    /// Gets the sole reason code representing an accepted line.
    /// </summary>
    internal const string AcceptanceReason = "structural-auto-property";
}

/// <summary>
/// Describes whether a line met the test-only structural policy.
/// </summary>
internal enum StructuralLineDisposition
{
    /// <summary>
    /// The line met every proof requirement.
    /// </summary>
    Accepted,

    /// <summary>
    /// The line did not meet at least one proof requirement.
    /// </summary>
    Rejected,
}

/// <summary>
/// Records the deterministic explanation for one changed coverage line.
/// </summary>
internal sealed record StructuralLineClassificationEntry(
    string PolicyIdentifier,
    string PolicyVersion,
    string Path,
    int Line,
    bool IsMeasured,
    bool? LineCovered,
    int? CoveredConditions,
    int? ValidConditions,
    string? SourceFingerprint,
    string? SourceProvenance,
    string? ParseOptionsIdentity,
    string? SourceTreePath,
    string? SymbolDocumentationId,
    string? SymbolDisplayName,
    StructuralLineDisposition Disposition,
    string ReasonCode,
    string? Metadata);

/// <summary>
/// Holds sorted, test-only audit evidence and a stable serialization for assertions.
/// </summary>
internal sealed record StructuralLineClassificationAudit(IReadOnlyList<StructuralLineClassificationEntry> Entries)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    /// Serializes the ordered in-memory audit without creating an artifact file.
    /// </summary>
    internal string ToDeterministicJson() => JsonSerializer.Serialize(Entries, SerializerOptions);
}

/// <summary>
/// Binds one explicit fixture source document to its bytes, syntax tree, compiler options, and provenance.
/// </summary>
internal sealed record StructuralSourceDocument(
    string Path,
    byte[] Bytes,
    string Fingerprint,
    CSharpParseOptions ParseOptions,
    SyntaxTree SyntaxTree,
    Encoding Encoding,
    bool IsGenerated = false,
    bool IsSynthesized = false)
{
    /// <summary>
    /// Creates an authored source document whose tree is parsed from the provided bytes and explicit decoder.
    /// </summary>
    internal static StructuralSourceDocument Create(
        string path,
        byte[] bytes,
        CSharpParseOptions parseOptions,
        Encoding? encoding = null,
        bool isGenerated = false,
        bool isSynthesized = false,
        string? fingerprint = null,
        SyntaxTree? syntaxTree = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(parseOptions);

        encoding ??= new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        var sourceText = SourceText.From(encoding.GetString(bytes), encoding, SourceHashAlgorithm.Sha256);
        syntaxTree ??= CSharpSyntaxTree.ParseText(sourceText, parseOptions, path);
        return new StructuralSourceDocument(
            path,
            bytes,
            fingerprint ?? Convert.ToHexString(SHA256.HashData(bytes)),
            parseOptions,
            syntaxTree,
            encoding,
            isGenerated,
            isSynthesized);
    }
}

/// <summary>
/// Resolves normalized fixture paths to their intentionally declared source documents.
/// </summary>
internal sealed class StructuralSourceManifest
{
    private readonly IReadOnlyList<StructuralSourceDocument> documents;

    /// <summary>
    /// Initializes the source manifest from explicitly declared fixture documents.
    /// </summary>
    internal StructuralSourceManifest(IEnumerable<StructuralSourceDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        this.documents = documents.ToArray();
    }

    /// <summary>
    /// Finds every document mapped to a normalized repository-relative path.
    /// </summary>
    internal IReadOnlyList<StructuralSourceDocument> Find(string normalizedPath) =>
        documents
            .Where(document => string.Equals(
                StructuralLineClassifier.NormalizePath(document.Path),
                normalizedPath,
                StringComparison.Ordinal))
            .ToArray();
}

/// <summary>
/// Classifies changed coverage lines against explicit fixture-only C# compiler evidence.
/// </summary>
internal sealed class StructuralLineClassifier
{
    private readonly Func<PatchCoverageLine, Exception?>? faultInjection;

    /// <summary>
    /// Initializes the classifier with an optional test seam that exercises the fail-closed fault boundary.
    /// </summary>
    internal StructuralLineClassifier(Func<PatchCoverageLine, Exception?>? faultInjection = null)
    {
        this.faultInjection = faultInjection;
    }

    /// <summary>
    /// Produces one audit entry per changed coverage line without mutating patch evidence or coverage results.
    /// </summary>
    internal StructuralLineClassificationAudit Classify(
        PatchCoverageAnalysis analysis,
        CSharpCompilation compilation,
        StructuralSourceManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(manifest);

        var entries = analysis.Lines
            .Select(line => ClassifyLine(line, compilation, manifest))
            .OrderBy(entry => entry.Path, StringComparer.Ordinal)
            .ThenBy(entry => entry.Line)
            .ThenBy(entry => entry.ReasonCode, StringComparer.Ordinal)
            .ToArray();
        return new StructuralLineClassificationAudit(entries);
    }

    /// <summary>
    /// Normalizes a fixture path without consulting the checkout or host filesystem.
    /// </summary>
    internal static string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return path.Replace('\\', '/');
    }

    private StructuralLineClassificationEntry ClassifyLine(
        PatchCoverageLine line,
        CSharpCompilation compilation,
        StructuralSourceManifest manifest)
    {
        StructuralSourceDocument? source = null;
        IPropertySymbol? propertySymbol = null;
        try
        {
            var normalizedPath = NormalizePath(line.Path);
            if (!string.Equals(Path.GetExtension(normalizedPath), ".cs", StringComparison.OrdinalIgnoreCase))
            {
                return Reject(line, normalizedPath, "not-csharp");
            }

            if (!line.IsMeasured)
            {
                return Reject(line, normalizedPath, "not-measured");
            }

            var documents = manifest.Find(normalizedPath);
            if (documents.Count == 0)
            {
                return Reject(line, normalizedPath, "source-missing");
            }

            if (documents.Count != 1)
            {
                return Reject(line, normalizedPath, "source-ambiguous");
            }

            source = documents[0];
            if (source.IsGenerated || source.IsSynthesized)
            {
                return Reject(line, normalizedPath, "source-generated", source);
            }

            if (!IsSourceIdentityValid(source, normalizedPath))
            {
                return Reject(line, normalizedPath, "source-fingerprint-mismatch", source);
            }

            if (!compilation.SyntaxTrees.Any(tree => ReferenceEquals(tree, source.SyntaxTree)))
            {
                return Reject(line, normalizedPath, "compilation-mismatch", source);
            }

            if (source.SyntaxTree.Options is not CSharpParseOptions syntaxTreeOptions)
            {
                return Reject(line, normalizedPath, "compilation-mismatch", source);
            }

            if (!HaveSameConditionalSymbols(source.ParseOptions, syntaxTreeOptions))
            {
                return Reject(line, normalizedPath, "conditional-compilation-mismatch", source);
            }

            if (!source.ParseOptions.Equals(syntaxTreeOptions))
            {
                return Reject(line, normalizedPath, "compilation-mismatch", source);
            }

            var sourceText = source.SyntaxTree.GetText();
            if (line.Line <= 0 || line.Line > sourceText.Lines.Count)
            {
                return Reject(line, normalizedPath, "location-unmatched", source);
            }

            var lineSpan = sourceText.Lines[line.Line - 1].Span;
            var properties = source.SyntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<PropertyDeclarationSyntax>()
                .Where(property => property.Span.OverlapsWith(lineSpan))
                .ToArray();
            if (properties.Length == 0)
            {
                return Reject(line, normalizedPath, "location-unmatched", source);
            }

            if (properties.Length != 1)
            {
                return Reject(line, normalizedPath, "location-ambiguous", source);
            }

            var property = properties[0];
            if (!HasNonBraceTokenOnLine(property, lineSpan))
            {
                return Reject(line, normalizedPath, "location-unmatched", source);
            }

            var injectedFailure = faultInjection?.Invoke(line);
            if (injectedFailure is not null)
            {
                throw injectedFailure;
            }

            var semanticModel = compilation.GetSemanticModel(source.SyntaxTree);
            propertySymbol = semanticModel.GetDeclaredSymbol(property);
            if (propertySymbol is null
                || propertySymbol.Type is IErrorTypeSymbol
                || propertySymbol.DeclaringSyntaxReferences.Length != 1
                || !ReferenceEquals(propertySymbol.DeclaringSyntaxReferences[0].SyntaxTree, source.SyntaxTree))
            {
                return Reject(line, normalizedPath, "property-unbound", source, propertySymbol);
            }

            if (propertySymbol.OverriddenProperty is not null
                || propertySymbol.ExplicitInterfaceImplementations.Length != 0
                || propertySymbol.ContainingType.TypeKind == TypeKind.Interface)
            {
                return Reject(line, normalizedPath, "property-inherited-or-overridden", source, propertySymbol);
            }

            if (property.Ancestors().OfType<TypeDeclarationSyntax>().Any(type =>
                type.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword))))
            {
                return Reject(line, normalizedPath, "partial-type", source, propertySymbol);
            }

            if (semanticModel.GetDiagnostics(property.Span).Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            {
                return Reject(line, normalizedPath, "semantic-diagnostic", source, propertySymbol);
            }

            var propertyShapeReason = GetPropertyShapeRejectionReason(property, propertySymbol);
            if (propertyShapeReason is not null)
            {
                return Reject(line, normalizedPath, propertyShapeReason, source, propertySymbol);
            }

            return Accept(line, normalizedPath, source, propertySymbol);
        }
        catch (Exception exception)
        {
            return Reject(
                line,
                NormalizePathForAudit(line.Path),
                "analysis-fault",
                source,
                propertySymbol,
                "exceptionType=" + exception.GetType().Name);
        }
    }

    private static bool IsSourceIdentityValid(StructuralSourceDocument source, string normalizedPath)
    {
        var fingerprint = Convert.ToHexString(SHA256.HashData(source.Bytes));
        if (!string.Equals(fingerprint, source.Fingerprint, StringComparison.Ordinal)
            || !string.Equals(NormalizePath(source.SyntaxTree.FilePath), normalizedPath, StringComparison.Ordinal))
        {
            return false;
        }

        return string.Equals(
            source.Encoding.GetString(source.Bytes),
            source.SyntaxTree.GetText().ToString(),
            StringComparison.Ordinal);
    }

    private static bool HaveSameConditionalSymbols(CSharpParseOptions expected, CSharpParseOptions actual) =>
        expected.PreprocessorSymbolNames
            .Order(StringComparer.Ordinal)
            .SequenceEqual(actual.PreprocessorSymbolNames.Order(StringComparer.Ordinal), StringComparer.Ordinal);

    private static bool HasNonBraceTokenOnLine(PropertyDeclarationSyntax property, TextSpan lineSpan)
    {
        return property.DescendantTokens(descendIntoTrivia: false)
            .Where(token => token.Span.OverlapsWith(lineSpan))
            .Any(token => !IsAccessorListDelimiter(property, token));
    }

    private static bool IsAccessorListDelimiter(PropertyDeclarationSyntax property, SyntaxToken token)
    {
        return property.AccessorList is { } accessors
            && (token.Equals(accessors.OpenBraceToken) || token.Equals(accessors.CloseBraceToken));
    }

    private static string? GetPropertyShapeRejectionReason(
        PropertyDeclarationSyntax property,
        IPropertySymbol propertySymbol)
    {
        if (propertySymbol.IsIndexer)
        {
            return "unsupported-property-shape";
        }

        if (property.AttributeLists.Count != 0)
        {
            return "property-attributes";
        }

        if (property.Initializer is not null)
        {
            return "property-initializer";
        }

        if (property.ExpressionBody is not null)
        {
            return "accessor-expression-body";
        }

        if (!property.Modifiers.All(IsAccessibilityModifier))
        {
            return "property-modifiers";
        }

        if (property.AccessorList is not { } accessors || accessors.Accessors.Count != 2)
        {
            return "unsupported-property-shape";
        }

        if (accessors.Accessors.Any(accessor => accessor.Modifiers.Count != 0))
        {
            return "accessor-modifiers";
        }

        if (accessors.Accessors.Any(accessor => accessor.Body is not null))
        {
            return "accessor-body";
        }

        if (accessors.Accessors.Any(accessor => accessor.ExpressionBody is not null))
        {
            return "accessor-expression-body";
        }

        if (accessors.Accessors.Any(accessor => accessor.SemicolonToken.IsMissing))
        {
            return "unsupported-property-shape";
        }

        var accessorKinds = accessors.Accessors.Select(accessor => accessor.Kind()).ToHashSet();
        return accessorKinds.SetEquals([SyntaxKind.GetAccessorDeclaration, SyntaxKind.SetAccessorDeclaration])
                || accessorKinds.SetEquals([SyntaxKind.GetAccessorDeclaration, SyntaxKind.InitAccessorDeclaration])
            ? null
            : "unsupported-property-shape";
    }

    private static bool IsAccessibilityModifier(SyntaxToken modifier)
    {
        return modifier.IsKind(SyntaxKind.PublicKeyword)
            || modifier.IsKind(SyntaxKind.PrivateKeyword)
            || modifier.IsKind(SyntaxKind.ProtectedKeyword)
            || modifier.IsKind(SyntaxKind.InternalKeyword);
    }

    private static StructuralLineClassificationEntry Accept(
        PatchCoverageLine line,
        string normalizedPath,
        StructuralSourceDocument source,
        IPropertySymbol propertySymbol)
    {
        return CreateEntry(
            line,
            normalizedPath,
            source,
            propertySymbol,
            StructuralLineDisposition.Accepted,
            StructuralLineClassificationPolicy.AcceptanceReason,
            null);
    }

    private static StructuralLineClassificationEntry Reject(
        PatchCoverageLine line,
        string normalizedPath,
        string reasonCode,
        StructuralSourceDocument? source = null,
        IPropertySymbol? propertySymbol = null,
        string? metadata = null)
    {
        return CreateEntry(
            line,
            normalizedPath,
            source,
            propertySymbol,
            StructuralLineDisposition.Rejected,
            reasonCode,
            metadata);
    }

    private static StructuralLineClassificationEntry CreateEntry(
        PatchCoverageLine line,
        string normalizedPath,
        StructuralSourceDocument? source,
        IPropertySymbol? propertySymbol,
        StructuralLineDisposition disposition,
        string reasonCode,
        string? metadata)
    {
        return new StructuralLineClassificationEntry(
            StructuralLineClassificationPolicy.Identifier,
            StructuralLineClassificationPolicy.Version,
            normalizedPath,
            line.Line,
            line.IsMeasured,
            line.LineCovered,
            line.CoveredConditions,
            line.ValidConditions,
            source?.Fingerprint,
            source is null ? null : source.IsGenerated || source.IsSynthesized ? "generated" : "authored",
            source is null ? null : FormatParseOptions(source.ParseOptions),
            source?.SyntaxTree.FilePath,
            propertySymbol?.GetDocumentationCommentId(),
            propertySymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            disposition,
            reasonCode,
            metadata);
    }

    private static string FormatParseOptions(CSharpParseOptions options)
    {
        return $"language={options.LanguageVersion};symbols={string.Join(',', options.PreprocessorSymbolNames.Order(StringComparer.Ordinal))}";
    }

    private static string NormalizePathForAudit(string? path) =>
        string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/');
}
