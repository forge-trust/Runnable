using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    /// Gets or sets a directory to override the resolved assembly directory for testing isolated fallback lookup.
    /// </summary>
    internal string? AssemblyDirectoryOverride { get; set; }

    /// <summary>
    /// Gets the path to the Tailwind CLI binary.
    /// </summary>
    /// <returns>The absolute path to the tailwindcss executable.</returns>
    /// <exception cref="FileNotFoundException">Thrown if the binary cannot be found in runtimes, local directory, or PATH.</exception>
    public virtual string GetTailwindPath()
    {
        var baseDir = BaseDirectoryOverride ?? AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(baseDir))
        {
            var rid = GetCurrentRid();

            // 1. Check standard NuGet runtime asset path (for published apps or project-local runtimes folder)
            var runtimePath = Path.Combine(baseDir, "runtimes", rid, "native", _binaryName);
            if (File.Exists(runtimePath))
            {
                _logger.LogDebug("Found Tailwind CLI at runtime path: {Path}", runtimePath);
                return runtimePath;
            }

            // 2. Check AppContext.BaseDirectory directly (for single-file or non-standard deployments)
            var localPath = Path.Combine(baseDir, _binaryName);
            if (File.Exists(localPath))
            {
                _logger.LogDebug("Found Tailwind CLI at base path: {Path}", localPath);
                return localPath;
            }

            // 3. Check for the binary in any runtimes folder relative to the assembly
            // This handles cases where runtime packages are present but not yet deployed to a flat bin folder
            var assemblyDir = AssemblyDirectoryOverride;
            if (string.IsNullOrEmpty(assemblyDir))
            {
                var assemblyLocation = typeof(TailwindCliManager).Assembly.Location;
                if (!string.IsNullOrEmpty(assemblyLocation))
                {
                    assemblyDir = Path.GetDirectoryName(assemblyLocation);
                }
            }

            if (!string.IsNullOrEmpty(assemblyDir))
            {
                var fallbackRuntimePath = Path.Combine(assemblyDir, "runtimes", rid, "native", _binaryName);
                if (File.Exists(fallbackRuntimePath))
                {
                    _logger.LogDebug("Found Tailwind CLI at fallback runtime path: {Path}", fallbackRuntimePath);
                    return fallbackRuntimePath;
                }

                var devRuntimeProjectPath = GetDevRuntimeProjectPath(baseDir, rid) ?? GetDevRuntimeProjectPath(assemblyDir, rid);
                if (!string.IsNullOrEmpty(devRuntimeProjectPath) && File.Exists(devRuntimeProjectPath))
                {
                    _logger.LogDebug("Found Tailwind CLI at development runtime project path: {Path}", devRuntimeProjectPath);
                    return devRuntimeProjectPath;
                }
            }
        }

        // 4. Check system PATH
        if (TryGetFromPath(_binaryName, out var path))
        {
            _logger.LogDebug("Found Tailwind CLI in PATH: {Path}", path);
            return path;
        }

        throw new FileNotFoundException(
            $"Tailwind CLI not found. Please ensure a 'ForgeTrust.Runnable.Web.Tailwind.Runtime.*' package for your platform is installed or tailwindcss is on PATH. (Missing: {_binaryName})",
            _binaryName);
    }

    /// <summary>
    /// Gets the Runtime Identifier (RID) for the current platform.
    /// </summary>
    /// <returns>The RID string (e.g., "win-x64", "linux-arm64").</returns>
    /// <remarks>
    /// Must be kept in sync with the RID logic in the runtime package projects and
    /// build/ForgeTrust.Runnable.Web.Tailwind.targets.
    /// </remarks>
    public static string GetCurrentRid()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return ResolveRid(OSPlatform.Windows, RuntimeInformation.ProcessArchitecture);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return ResolveRid(OSPlatform.Linux, RuntimeInformation.ProcessArchitecture);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return ResolveRid(OSPlatform.OSX, RuntimeInformation.ProcessArchitecture);
        }

        return "unknown";
    }

    /// <summary>
    /// Resolves the Tailwind runtime identifier for a specific platform and process architecture.
    /// </summary>
    /// <param name="osPlatform">The operating system platform to evaluate.</param>
    /// <param name="architecture">The process architecture to map.</param>
    /// <returns>The Tailwind runtime identifier for the supplied platform/architecture pair.</returns>
    /// <remarks>
    /// Must be kept in sync with the RID logic in the runtime package projects and
    /// build/ForgeTrust.Runnable.Web.Tailwind.targets.
    /// </remarks>
    internal static string ResolveRid(OSPlatform osPlatform, Architecture architecture)
    {
        if (osPlatform == OSPlatform.Windows)
        {
            return architecture switch
            {
                Architecture.X64 => "win-x64",
                // Tailwind v4.1.18 ships only a Windows x64 standalone binary.
                Architecture.Arm64 => "win-x64",
                _ => "unknown"
            };
        }

        if (osPlatform == OSPlatform.Linux)
        {
            return architecture switch
            {
                Architecture.X64 => "linux-x64",
                Architecture.Arm64 => "linux-arm64",
                _ => "unknown"
            };
        }

        if (osPlatform == OSPlatform.OSX)
        {
            return architecture switch
            {
                Architecture.X64 => "osx-x64",
                Architecture.Arm64 => "osx-arm64",
                _ => "unknown"
            };
        }

        return "unknown";
    }

    private static string? GetDevRuntimeProjectPath(string sourceDir, string rid)
    {
        var targetFramework = Path.GetFileName(sourceDir);
        var configurationDir = Directory.GetParent(sourceDir);
        var binDir = configurationDir?.Parent;

        if (string.IsNullOrEmpty(targetFramework) || configurationDir == null || binDir?.Name != "bin")
        {
            return null;
        }

        var binaryName = rid switch
        {
            "win-x64" => "tailwindcss-windows-x64.exe",
            "osx-arm64" => "tailwindcss-macos-arm64",
            "osx-x64" => "tailwindcss-macos-x64",
            "linux-arm64" => "tailwindcss-linux-arm64",
            "linux-x64" => "tailwindcss-linux-x64",
            _ => null
        };

        if (string.IsNullOrEmpty(binaryName))
        {
            return null;
        }

        foreach (var candidateRoot in EnumerateSelfAndAncestors(binDir.Parent))
        {
            var candidatePath = Path.Combine(
                candidateRoot,
                "Web",
                "ForgeTrust.Runnable.Web.Tailwind",
                "runtimes",
                "obj",
                $"ForgeTrust.Runnable.Web.Tailwind.Runtime.{rid}",
                configurationDir.Name,
                targetFramework,
                binaryName);

            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSelfAndAncestors(DirectoryInfo? startDirectory)
    {
        for (var current = startDirectory; current != null; current = current.Parent)
        {
            yield return current.FullName;
        }
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
