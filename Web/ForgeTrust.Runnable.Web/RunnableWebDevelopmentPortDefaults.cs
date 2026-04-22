using System.Globalization;
using System.Text;

namespace ForgeTrust.Runnable.Web;

/// <summary>
/// Resolves a deterministic development port for Runnable web hosts when the caller has not already supplied
/// an explicit port or URL configuration.
/// </summary>
internal static class RunnableWebDevelopmentPortDefaults
{
    private const int DevelopmentPortBase = 5600;
    private const int DevelopmentPortRange = 1000;

    /// <summary>
    /// Applies a deterministic <c>--port</c> fallback when neither command-line arguments nor environment
    /// variables specify where the host should listen.
    /// </summary>
    /// <param name="args">The command-line arguments supplied by the caller.</param>
    /// <param name="currentDirectory">The current working directory for the process.</param>
    /// <param name="applicationBaseDirectory">The application base directory for the host entry assembly.</param>
    /// <param name="environmentReader">Reads environment variables needed to detect explicit URL configuration.</param>
    /// <returns>
    /// A resolution describing the effective arguments. If no fallback was needed, the returned arguments match
    /// the supplied <paramref name="args"/>.
    /// </returns>
    internal static RunnableWebDevelopmentPortResolution Resolve(
        string[] args,
        string currentDirectory,
        string applicationBaseDirectory,
        Func<string, string?> environmentReader)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(currentDirectory);
        ArgumentNullException.ThrowIfNull(applicationBaseDirectory);
        ArgumentNullException.ThrowIfNull(environmentReader);

        if (HasExplicitUrlConfiguration(args, environmentReader))
        {
            return new(args, null, null);
        }

        var seedPath = ResolveSeedPath(currentDirectory, applicationBaseDirectory);
        var port = ComputeDeterministicPort(seedPath);

        return new([.. args, "--port", port.ToString(CultureInfo.InvariantCulture)], port, seedPath);
    }

    private static bool HasExplicitUrlConfiguration(
        IReadOnlyList<string> args,
        Func<string, string?> environmentReader)
    {
        foreach (var arg in args)
        {
            if (string.Equals(arg, "--port", StringComparison.OrdinalIgnoreCase)
                || arg.StartsWith("--port=", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--urls", StringComparison.OrdinalIgnoreCase)
                || arg.StartsWith("--urls=", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return !string.IsNullOrWhiteSpace(environmentReader("ASPNETCORE_URLS"))
               || !string.IsNullOrWhiteSpace(environmentReader("URLS"));
    }

    private static string ResolveSeedPath(string currentDirectory, string applicationBaseDirectory)
    {
        var repoRoot = FindAncestorContainingMarker(
            currentDirectory,
            directoryPath =>
                Directory.Exists(Path.Combine(directoryPath, ".git"))
                || File.Exists(Path.Combine(directoryPath, ".git")));
        if (repoRoot is not null)
        {
            return NormalizePath(repoRoot);
        }

        var projectRoot = FindAncestorContainingMarker(
            applicationBaseDirectory,
            directoryPath => Directory.EnumerateFiles(directoryPath, "*.csproj").Any());
        if (projectRoot is not null)
        {
            return NormalizePath(projectRoot);
        }

        return NormalizePath(currentDirectory);
    }

    private static int ComputeDeterministicPort(string seedPath)
    {
        var hash = ComputeFnv1aHash(seedPath);
        return DevelopmentPortBase + (int)(hash % DevelopmentPortRange);
    }

    private static string? FindAncestorContainingMarker(
        string startPath,
        Func<string, bool> predicate)
    {
        var fullPath = Path.GetFullPath(startPath);
        var current = Directory.Exists(fullPath)
            ? new DirectoryInfo(fullPath)
            : new DirectoryInfo(Path.GetDirectoryName(fullPath) ?? fullPath);

        while (current is not null)
        {
            if (predicate(current.FullName))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static string NormalizePath(string path)
    {
        var normalized = Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (OperatingSystem.IsWindows())
        {
            normalized = normalized.ToUpperInvariant();
        }

        return normalized.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private static uint ComputeFnv1aHash(string value)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;

        var hash = offsetBasis;
        foreach (var currentByte in Encoding.UTF8.GetBytes(value))
        {
            hash ^= currentByte;
            hash *= prime;
        }

        return hash;
    }
}

/// <summary>
/// Describes the effective command-line arguments after Runnable web development defaults have been resolved.
/// </summary>
/// <param name="Args">The effective arguments that should be passed into host startup.</param>
/// <param name="AppliedPort">The fallback port applied by the resolver, if any.</param>
/// <param name="SeedPath">The normalized workspace or project path used to compute the fallback port.</param>
internal readonly record struct RunnableWebDevelopmentPortResolution(
    string[] Args,
    int? AppliedPort,
    string? SeedPath);
