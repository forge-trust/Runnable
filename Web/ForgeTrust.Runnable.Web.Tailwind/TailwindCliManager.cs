namespace ForgeTrust.Runnable.Web.Tailwind;

using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

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
    /// Gets the path to the Tailwind CLI binary.
    /// </summary>
    /// <returns>The absolute path to the binary, or null if not found.</returns>
    public string? GetTailwindPath()
    {
        var assemblyLocation = typeof(TailwindCliManager).Assembly.Location;
        var assemblyDir = Path.GetDirectoryName(assemblyLocation);

        if (assemblyDir != null)
        {
            var rid = GetCurrentRid();
            // Standard NuGet runtime asset path
            var runtimePath = Path.Combine(assemblyDir, "runtimes", rid, "native", _binaryName);
            if (File.Exists(runtimePath))
            {
                _logger.LogDebug("Found Tailwind CLI at runtime path: {Path}", runtimePath);
                return runtimePath;
            }

            // Local development fallback
            var localPath = Path.Combine(assemblyDir, _binaryName);
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
            $"Tailwind standalone CLI binary '{_binaryName}' not found. " +
            "Ensure the ForgeTrust.Runnable.Web.Tailwind.Runtime package is installed or tailwindcss is available in your PATH.");
    }

    private static string GetCurrentRid()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "win-x64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "linux-x64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";

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
