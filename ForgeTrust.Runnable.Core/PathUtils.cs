namespace ForgeTrust.Runnable.Core;

public static class PathUtils
{
    /// <summary>
    /// Traverses upward from the starting path to find the repository root (directory containing .git).
    /// </summary>
    public static string FindRepositoryRoot(string startPath)
    {
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
