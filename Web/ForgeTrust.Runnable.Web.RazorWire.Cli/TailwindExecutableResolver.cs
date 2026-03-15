using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

internal interface ITailwindExecutableResolver
{
    Task<string> ResolveAsync(TailwindExecutableRequest request, CancellationToken cancellationToken);
}

internal sealed class TailwindExecutableResolver : ITailwindExecutableResolver
{
    private readonly ILogger<TailwindExecutableResolver> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public TailwindExecutableResolver(
        ILogger<TailwindExecutableResolver> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public async Task<string> ResolveAsync(TailwindExecutableRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.IsNullOrWhiteSpace(request.ExecutablePath))
        {
            var explicitPath = Path.GetFullPath(request.ExecutablePath);
            if (!File.Exists(explicitPath))
            {
                throw new FileNotFoundException("The configured Tailwind executable path does not exist.", explicitPath);
            }

            return explicitPath;
        }

        var cacheRoot = string.IsNullOrWhiteSpace(request.InstallDirectory)
            ? GetDefaultInstallDirectory()
            : Path.GetFullPath(request.InstallDirectory);
        var assetName = GetAssetName();
        var destinationDirectory = Path.Combine(cacheRoot, request.Version, assetName);
        var executablePath = Path.Combine(destinationDirectory, assetName);

        if (File.Exists(executablePath))
        {
            return executablePath;
        }

        Directory.CreateDirectory(destinationDirectory);

        var downloadUrl =
            $"https://github.com/tailwindlabs/tailwindcss/releases/download/v{request.Version}/{assetName}";
        var tempPath = Path.Combine(destinationDirectory, $"{assetName}.download");

        _logger.LogInformation("Downloading Tailwind standalone CLI from {DownloadUrl}", downloadUrl);

        var client = _httpClientFactory.CreateClient("TailwindDownloader");
        await using (var stream = await client.GetStreamAsync(downloadUrl, cancellationToken))
        await using (var fileStream = File.Create(tempPath))
        {
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        if (File.Exists(executablePath))
        {
            File.Delete(executablePath);
        }

        File.Move(tempPath, executablePath);
        EnsureExecutablePermissions(executablePath);

        return executablePath;
    }

    internal static string GetDefaultInstallDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ForgeTrust",
                "Runnable",
                "tailwindcss");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            return Path.Combine(Path.GetTempPath(), "forge-trust", "runnable", "tailwindcss");
        }

        return Path.Combine(home, ".cache", "forge-trust", "runnable", "tailwindcss");
    }

    internal static string GetAssetName()
    {
        var architecture = RuntimeInformation.OSArchitecture;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return architecture switch
            {
                Architecture.X64 => "tailwindcss-windows-x64.exe",
                Architecture.Arm64 => "tailwindcss-windows-arm64.exe",
                _ => throw new PlatformNotSupportedException(
                    $"Tailwind standalone CLI is not supported on Windows {architecture}.")
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return architecture switch
            {
                Architecture.X64 => "tailwindcss-macos-x64",
                Architecture.Arm64 => "tailwindcss-macos-arm64",
                _ => throw new PlatformNotSupportedException(
                    $"Tailwind standalone CLI is not supported on macOS {architecture}.")
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return architecture switch
            {
                Architecture.X64 => "tailwindcss-linux-x64",
                Architecture.Arm64 => "tailwindcss-linux-arm64",
                _ => throw new PlatformNotSupportedException(
                    $"Tailwind standalone CLI is not supported on Linux {architecture}.")
            };
        }

        throw new PlatformNotSupportedException("Tailwind standalone CLI is only supported on Windows, macOS, and Linux.");
    }

    private static void EnsureExecutablePermissions(string executablePath)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(
            executablePath,
            UnixFileMode.UserRead
            | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead
            | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead
            | UnixFileMode.OtherExecute);
    }
}
