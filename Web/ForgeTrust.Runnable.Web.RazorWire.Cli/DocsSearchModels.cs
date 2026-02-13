namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

/// <summary>
/// Represents a single searchable docs record.
/// </summary>
public sealed record DocSearchRecord(
    string Id,
    string Path,
    string Title,
    IReadOnlyList<string> Headings,
    string BodyText,
    string Snippet);

/// <summary>
/// Represents metadata for a docs search index output.
/// </summary>
public sealed record DocSearchIndexMetadata(
    string GeneratedAtUtc,
    string Version,
    string Engine);

/// <summary>
/// Root JSON payload for the docs search index.
/// </summary>
public sealed record DocSearchIndexDocument(
    DocSearchIndexMetadata Metadata,
    IReadOnlyList<DocSearchRecord> Documents);
