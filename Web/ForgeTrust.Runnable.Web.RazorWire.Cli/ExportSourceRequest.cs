namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

internal enum ExportSourceKind
{
    Url,
    Project,
    Dll
}

internal sealed record ExportSourceRequest(
    ExportSourceKind SourceKind,
    string SourceValue,
    IReadOnlyList<string> AppArgs);
