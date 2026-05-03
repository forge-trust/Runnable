using ForgeTrust.Runnable.Web.RazorDocs.Models;

namespace ForgeTrust.Runnable.Web.RazorDocs.Services;

internal sealed record RazorDocsMetadataDiagnostic(
    string Code,
    string FieldPath,
    string Problem,
    string Cause,
    string Fix);

internal sealed record MarkdownMetadataParseResult(
    DocMetadata? Metadata,
    IReadOnlyList<RazorDocsMetadataDiagnostic> Diagnostics);
