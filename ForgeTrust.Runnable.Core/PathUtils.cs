namespace ForgeTrust.Runnable.Core;

public static class PathUtils
{
    /// <summary>
    /// Traverses upward from the starting path to find the repository root (directory containing .git).
    /// <summary>
    /// Locate the nearest ancestor directory (starting at <paramref name="startPath"/>) that contains a `.git` directory or file.
    /// </summary>
    /// <param name="startPath">The path from which to begin searching upward for a repository root.</param>
    /// <returns>The full path of the nearest ancestor directory containing a `.git` directory or file, or the original <paramref name="startPath"/> if none is found.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="startPath"/> is null, empty, or consists only of whitespace.</exception>
    public static string FindRepositoryRoot(string startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath))
        {
            throw new ArgumentException("Start path cannot be null or whitespace.", nameof(startPath));
        }

        var current = new DirectoryInfo(startPath);

        // If startPath doesn't exist, walk up until we find one that does
        while (current is { Exists: false })
        {
            current = current.Parent;
        }

        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git"))
                || File.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return startPath;
    }
}