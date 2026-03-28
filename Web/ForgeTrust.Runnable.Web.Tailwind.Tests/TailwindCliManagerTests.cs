using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using FakeItEasy;
using ForgeTrust.Runnable.Web.Tailwind;

namespace ForgeTrust.Runnable.Web.Tailwind.Tests;

public class TailwindCliManagerTests : IDisposable
{
    private readonly string _tempPath;
    private readonly ILogger<TailwindCliManager> _logger;
    private readonly TailwindCliManager _manager;
    private readonly string _binaryName;

    public TailwindCliManagerTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), "TailwindTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempPath);
        _logger = A.Fake<ILogger<TailwindCliManager>>();
        _manager = new TailwindCliManager(_logger);
        _manager.BaseDirectoryOverride = _tempPath;
        _binaryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "tailwindcss.exe" : "tailwindcss";
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
        {
            Directory.Delete(_tempPath, true);
        }
    }

    [Fact]
    public void GetTailwindPath_ReturnsRuntimePath_IfFound()
    {
        // Arrange
        var rid = GetCurrentRid();
        var runtimeNativeDir = Path.Combine(_tempPath, "runtimes", rid, "native");
        Directory.CreateDirectory(runtimeNativeDir);
        var expectedPath = Path.Combine(runtimeNativeDir, _binaryName);
        File.WriteAllText(expectedPath, "dummy");

        // Act
        var result = _manager.GetTailwindPath();

        // Assert
        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void GetTailwindPath_ReturnsLocalPath_IfRuntimeNotFound()
    {
        // Arrange
        var expectedPath = Path.Combine(_tempPath, _binaryName);
        File.WriteAllText(expectedPath, "dummy");

        // Act
        var result = _manager.GetTailwindPath();

        // Assert
        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void GetTailwindPath_ThrowsFileNotFoundException_IfNotFoundAnywhere()
    {
        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => _manager.GetTailwindPath());
    }

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
}
