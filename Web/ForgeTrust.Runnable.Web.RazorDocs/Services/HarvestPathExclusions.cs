namespace ForgeTrust.Runnable.Web.RazorDocs.Services;

internal static class HarvestPathExclusions
{
    // Explicitly excluded regardless of hidden-directory allowlist behavior.
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules",
        "bin",
        "obj",
        "Tests"
    };

    // Hidden directories to include even though dot-prefixed directories are excluded by default.
    private static readonly HashSet<string> AllowedHiddenDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
    };

    // File paths should be filtered by directory segments only so dot-prefixed files are still included.
    public static bool ShouldExcludeFilePath(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        var segments = filePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/', '\\'],
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length <= 1)
        {
            return false;
        }

        foreach (var directorySegment in segments[..^1])
        {
            if (ExcludedDirectories.Contains(directorySegment))
            {
                return true;
            }

            if (directorySegment.StartsWith('.') && !AllowedHiddenDirectories.Contains(directorySegment))
            {
                return true;
            }
        }

        return false;
    }
}
