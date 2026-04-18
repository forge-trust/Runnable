using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using FakeItEasy;
using ForgeTrust.Runnable.Web.Tailwind;

namespace ForgeTrust.Runnable.Web.Tailwind.Tests;

public class TailwindCliManagerTests : IDisposable
{
    private readonly string _tempPath;
    private readonly string? _originalPath;
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
        _originalPath = Environment.GetEnvironmentVariable("PATH");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("PATH", _originalPath);

        if (Directory.Exists(_tempPath))
        {
            Directory.Delete(_tempPath, true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void GetTailwindPath_ReturnsRuntimePath_IfFound()
    {
        // Arrange
        var rid = TailwindCliManager.GetCurrentRid();
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
    public void GetTailwindPath_ReturnsPathValue_IfFoundInPath()
    {
        // Arrange
        var pathDir = Path.Combine(_tempPath, "bin");
        Directory.CreateDirectory(pathDir);

        var expectedPath = Path.Combine(pathDir, _binaryName);
        File.WriteAllText(expectedPath, "dummy");
        Environment.SetEnvironmentVariable("PATH", string.Join(Path.PathSeparator, [pathDir, _originalPath ?? string.Empty]));

        // Act
        var result = _manager.GetTailwindPath();

        // Assert
        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void GetTailwindPath_ReturnsAssemblyFallbackRuntimePath_IfFound()
    {
        var rid = TailwindCliManager.GetCurrentRid();
        var baseDir = Path.Combine(_tempPath, "app-base");
        var assemblyDir = Path.Combine(_tempPath, "assembly-base");
        Directory.CreateDirectory(baseDir);
        _manager.BaseDirectoryOverride = baseDir;
        _manager.AssemblyDirectoryOverride = assemblyDir;

        var runtimeNativeDir = Path.Combine(assemblyDir, "runtimes", rid, "native");
        Directory.CreateDirectory(runtimeNativeDir);
        var expectedPath = Path.Combine(runtimeNativeDir, _binaryName);

        File.WriteAllText(expectedPath, "dummy");

        var result = _manager.GetTailwindPath();

        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void GetTailwindPath_ThrowsFileNotFoundException_IfNotFoundAnywhere()
    {
        Environment.SetEnvironmentVariable("PATH", string.Empty);

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => _manager.GetTailwindPath());
    }

    [Theory]
    [MemberData(nameof(RidMatrixCases))]
    public void ResolveRid_ReturnsExpectedMapping(OSPlatform osPlatform, Architecture architecture, string expectedRid)
    {
        var result = TailwindCliManager.ResolveRid(osPlatform, architecture);

        Assert.Equal(expectedRid, result);
    }

    public static IEnumerable<object[]> RidMatrixCases()
    {
        yield return [OSPlatform.Windows, Architecture.X64, "win-x64"];
        yield return [OSPlatform.Windows, Architecture.Arm64, "win-x64"];
        yield return [OSPlatform.Linux, Architecture.X64, "linux-x64"];
        yield return [OSPlatform.Linux, Architecture.Arm64, "linux-arm64"];
        yield return [OSPlatform.OSX, Architecture.X64, "osx-x64"];
        yield return [OSPlatform.OSX, Architecture.Arm64, "osx-arm64"];
        yield return [OSPlatform.Create("FREEBSD"), Architecture.X64, "unknown"];
    }
}
