using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Core;

/// <summary>
/// Provides utility methods for operations on file and directory paths.
/// </summary>
public static partial class PathUtils
{
    /// <summary>
    /// Locates the nearest ancestor directory (starting at <paramref name="startPath"/>) that contains a `.git` directory or file, effectively identifying the repository root.
    /// </summary>
    /// <param name="startPath">The path from which to begin searching upward for a repository root; may refer to a file or directory.</param>
    /// <returns>The full path of the nearest ancestor directory containing a `.git` directory or file, or the original <paramref name="startPath"/> if none is found.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="startPath"/> is null, empty, or consists only of whitespace.</exception>
    public static string FindRepositoryRoot(string startPath)
    {
        return FindRepositoryRootCore(startPath, logger: null);
    }

    /// <summary>
    /// Locates the nearest ancestor directory (starting at <paramref name="startPath"/>) that contains a `.git` directory or file, effectively identifying the repository root.
    /// </summary>
    /// <param name="startPath">The path from which to begin searching upward for a repository root; may refer to a file or directory.</param>
    /// <param name="logger">
    /// Logger for diagnostic warnings when repository-root discovery has to recover from a path that does not exist.
    /// </param>
    /// <returns>The full path of the nearest ancestor directory containing a `.git` directory or file, or the original <paramref name="startPath"/> if none is found.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="startPath"/> is null, empty, or consists only of whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is null.</exception>
    public static string FindRepositoryRoot(string startPath, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        return FindRepositoryRootCore(startPath, logger);
    }

    private static string FindRepositoryRootCore(string startPath, ILogger? logger)
    {
        if (string.IsNullOrWhiteSpace(startPath))
        {
            throw new ArgumentException("Start path cannot be null or whitespace.", nameof(startPath));
        }

        var fileExists = File.Exists(startPath);
        DirectoryInfo? current = fileExists
            ? new DirectoryInfo(GetExistingFileDirectory(startPath))
            : new DirectoryInfo(startPath);
        var fallbackFromMissingPath = !fileExists && !current.Exists;

        while (current is { Exists: false })
        {
            current = current.Parent;
        }

        if (fallbackFromMissingPath)
        {
            if (logger != null && current != null)
            {
                LogMissingPathFallback(logger, startPath, current.FullName);
            }
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

    [ExcludeFromCodeCoverage(
        Justification = "File.Exists excludes root directories; the fallback preserves defensive handling for unusual path providers.")]
    private static string GetExistingFileDirectory(string startPath)
    {
        var fullPath = Path.GetFullPath(startPath);
        return Path.GetDirectoryName(fullPath)
            ?? Path.GetPathRoot(fullPath)
            ?? fullPath;
    }

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Repository-root search started from missing path {StartPath}; continuing from nearest existing ancestor {FallbackPath}.")]
    private static partial void LogMissingPathFallback(ILogger logger, string startPath, string fallbackPath);

}
