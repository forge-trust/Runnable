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

    public static bool ShouldExclude(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/'],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            if (ExcludedDirectories.Contains(segment))
            {
                return true;
            }

            if (segment.StartsWith('.') && !AllowedHiddenDirectories.Contains(segment))
            {
                return true;
            }
        }

        return false;
    }
}
