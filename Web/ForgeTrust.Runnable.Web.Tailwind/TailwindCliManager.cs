using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("ForgeTrust.Runnable.Web.Tailwind.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace ForgeTrust.Runnable.Web.Tailwind;

/// <summary>
/// Manages the location and execution of the Tailwind CLI binary.
/// </summary>
public class TailwindCliManager
{
    private readonly ILogger<TailwindCliManager> _logger;
    private readonly string _binaryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "tailwindcss.exe" : "tailwindcss";

    /// <summary>
    /// Initializes a new instance of the <see cref="TailwindCliManager"/> class.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    public TailwindCliManager(ILogger<TailwindCliManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets or sets a directory to override <see cref="AppContext.BaseDirectory"/> for testing.
    /// </summary>
    internal string? BaseDirectoryOverride { get; set; }

    /// <summary>
    /// Gets the path to the Tailwind CLI binary.
    /// </summary>
    /// <returns>The absolute path to the binary, or null if not found.</returns>
    public virtual string GetTailwindPath()
    {
        var baseDir = BaseDirectoryOverride ?? AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(baseDir))
        {
            var rid = GetCurrentRid();
            // Standard NuGet runtime asset path
            var runtimePath = Path.Combine(baseDir, "runtimes", rid, "native", _binaryName);
            if (File.Exists(runtimePath))
            {
                _logger.LogDebug("Found Tailwind CLI at runtime path: {Path}", runtimePath);
                return runtimePath;
            }

            // Local development fallback
            var localPath = Path.Combine(baseDir, _binaryName);
            if (File.Exists(localPath))
            {
                _logger.LogDebug("Found Tailwind CLI at local path: {Path}", localPath);
                return localPath;
            }
        }

        // Check system PATH
        if (TryGetFromPath(_binaryName, out var path))
        {
            _logger.LogDebug("Found Tailwind CLI in PATH: {Path}", path);
            return path;
        }

        throw new FileNotFoundException(
            $"Tailwind CLI not found at any of the expected locations. Please ensure 'ForgeTrust.Runnable.Web.Tailwind' is added to your project or tailwindcss is on PATH. (Missing: {_binaryName})",
            _binaryName);
    }

    /// <summary>
    /// Gets the current runtime identifier.
    /// </summary>
    /// <remarks>
    /// Must be kept in sync with the RID logic in ForgeTrust.Runnable.Web.Tailwind.csproj and targets.
    /// </remarks>
    private static string GetCurrentRid()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "win-arm64" : "win-x64";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        }

        return "unknown";
    }

    private static bool TryGetFromPath(string fileName, out string path)
    {
        path = string.Empty;
        var values = Environment.GetEnvironmentVariable("PATH");
        if (values == null) return false;

        foreach (var p in values.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(p, fileName);
            if (File.Exists(fullPath))
            {
                path = fullPath;
                return true;
            }
        }
        return false;
    }
}
