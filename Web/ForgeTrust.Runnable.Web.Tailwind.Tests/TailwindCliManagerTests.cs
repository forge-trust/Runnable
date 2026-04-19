using System.Runtime.InteropServices;
using FakeItEasy;
using ForgeTrust.Runnable.Web.Tailwind;
using Microsoft.Extensions.Logging;

namespace ForgeTrust.Runnable.Web.Tailwind.Tests;

[CollectionDefinition("EnvVarIsolation", DisableParallelization = true)]
public sealed class EnvVarIsolationCollection;

[Collection("EnvVarIsolation")]
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
        _manager.AssemblyDirectoryOverride = Path.Combine(_tempPath, "isolated-assembly");
        _binaryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "tailwindcss.exe" : "tailwindcss";
        _originalPath = Environment.GetEnvironmentVariable("PATH");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("PATH", _originalPath);
        TailwindCliManager.IsOSPlatformOverride = null;
        TailwindCliManager.ProcessArchitectureOverride = null;

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
    public void GetTailwindPath_UsesWindowsBinaryName_WhenWindowsOverrideIsSet()
    {
        var pathDir = Path.Combine(_tempPath, "bin");
        Directory.CreateDirectory(pathDir);
        var expectedPath = Path.Combine(pathDir, "tailwindcss.exe");
        File.WriteAllText(expectedPath, "dummy");
        Environment.SetEnvironmentVariable("PATH", string.Join(Path.PathSeparator, [pathDir, _originalPath ?? string.Empty]));
        TailwindCliManager.IsOSPlatformOverride = platform => platform == OSPlatform.Windows;

        var manager = new TailwindCliManager(_logger)
        {
            BaseDirectoryOverride = _tempPath,
            AssemblyDirectoryOverride = _tempPath
        };

        var result = manager.GetTailwindPath();

        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void GetTailwindPath_ResolvesBinary_WhenUsingDefaultBaseDirectory()
    {
        var pathDir = Path.Combine(_tempPath, "bin");
        Directory.CreateDirectory(pathDir);

        var expectedPath = Path.Combine(pathDir, _binaryName);
        File.WriteAllText(expectedPath, "dummy");
        Environment.SetEnvironmentVariable("PATH", string.Join(Path.PathSeparator, [pathDir, _originalPath ?? string.Empty]));

        _manager.BaseDirectoryOverride = null;
        _manager.AssemblyDirectoryOverride = null;

        var result = _manager.GetTailwindPath();

        Assert.True(Path.IsPathRooted(result));
        Assert.True(File.Exists(result));
        Assert.Contains("tailwindcss", Path.GetFileName(result), StringComparison.Ordinal);
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
    public void GetTailwindPath_ReturnsDevelopmentRuntimeProjectPath_IfFound()
    {
        var rid = TailwindCliManager.GetCurrentRid();
        var baseDir = Path.Combine(_tempPath, "examples", "sample-app", "bin", "Debug", "net10.0");
        var assemblyDir = Path.Combine(baseDir, "assembly-shadow");
        Directory.CreateDirectory(baseDir);
        _manager.BaseDirectoryOverride = baseDir;
        _manager.AssemblyDirectoryOverride = assemblyDir;

        var runtimeProjectDir = Path.Combine(
            _tempPath,
            "Web",
            "ForgeTrust.Runnable.Web.Tailwind",
            "runtimes",
            "obj",
            $"ForgeTrust.Runnable.Web.Tailwind.Runtime.{rid}",
            "Debug",
            "net10.0");
        Directory.CreateDirectory(runtimeProjectDir);
        var expectedPath = Path.Combine(runtimeProjectDir, GetRuntimeProjectBinaryName(rid));
        File.WriteAllText(expectedPath, "dummy");

        var result = _manager.GetTailwindPath();

        Assert.Equal(expectedPath, result);
    }

    [Theory]
    [MemberData(nameof(DevelopmentRuntimeRidCases))]
    public void GetTailwindPath_ReturnsDevelopmentRuntimeProjectPath_ForSupportedRidOverride(string rid, string binaryName)
    {
        var baseDir = Path.Combine(_tempPath, "examples", "sample-app", "bin", "Debug", "net10.0");
        var assemblyDir = Path.Combine(baseDir, "assembly-shadow");
        Directory.CreateDirectory(baseDir);
        _manager.BaseDirectoryOverride = baseDir;
        _manager.AssemblyDirectoryOverride = assemblyDir;
        _manager.RidOverride = rid;

        var runtimeProjectDir = Path.Combine(
            _tempPath,
            "Web",
            "ForgeTrust.Runnable.Web.Tailwind",
            "runtimes",
            "obj",
            $"ForgeTrust.Runnable.Web.Tailwind.Runtime.{rid}",
            "Debug",
            "net10.0");
        Directory.CreateDirectory(runtimeProjectDir);
        var expectedPath = Path.Combine(runtimeProjectDir, binaryName);
        File.WriteAllText(expectedPath, "dummy");

        var result = _manager.GetTailwindPath();

        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void GetTailwindPath_ReturnsDevelopmentRuntimeProjectPath_FromAssemblyDirectory_WhenBaseDirectoryShapeIsUnsupported()
    {
        var rid = TailwindCliManager.GetCurrentRid();
        var baseDir = Path.Combine(_tempPath, "examples", "sample-app");
        var assemblyDir = Path.Combine(_tempPath, "examples", "shadow-app", "bin", "Debug", "net10.0");
        Directory.CreateDirectory(baseDir);
        Directory.CreateDirectory(assemblyDir);
        _manager.BaseDirectoryOverride = baseDir;
        _manager.AssemblyDirectoryOverride = assemblyDir;

        var runtimeProjectDir = Path.Combine(
            _tempPath,
            "Web",
            "ForgeTrust.Runnable.Web.Tailwind",
            "runtimes",
            "obj",
            $"ForgeTrust.Runnable.Web.Tailwind.Runtime.{rid}",
            "Debug",
            "net10.0");
        Directory.CreateDirectory(runtimeProjectDir);
        var expectedPath = Path.Combine(runtimeProjectDir, GetRuntimeProjectBinaryName(rid));
        File.WriteAllText(expectedPath, "dummy");

        var result = _manager.GetTailwindPath();

        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void GetTailwindPath_ThrowsFileNotFoundException_WhenSupportedRidHasNoDevelopmentRuntimeProjectInAncestorTree()
    {
        var rid = TailwindCliManager.GetCurrentRid();
        var baseDir = Path.Combine(_tempPath, "examples", "sample-app", "bin", "Debug", "net10.0");
        var assemblyDir = Path.Combine(_tempPath, "shadow", "bin", "Debug", "net10.0");
        Directory.CreateDirectory(baseDir);
        Directory.CreateDirectory(assemblyDir);
        _manager.BaseDirectoryOverride = baseDir;
        _manager.AssemblyDirectoryOverride = assemblyDir;
        _manager.RidOverride = rid;
        Environment.SetEnvironmentVariable("PATH", string.Empty);

        Assert.Throws<FileNotFoundException>(() => _manager.GetTailwindPath());
    }

    [Fact]
    public void GetTailwindPath_ThrowsFileNotFoundException_WhenRidOverrideIsUnsupported()
    {
        var baseDir = Path.Combine(_tempPath, "examples", "sample-app", "bin", "Debug", "net10.0");
        var assemblyDir = Path.Combine(baseDir, "assembly-shadow");
        Directory.CreateDirectory(baseDir);
        _manager.BaseDirectoryOverride = baseDir;
        _manager.AssemblyDirectoryOverride = assemblyDir;
        _manager.RidOverride = "mystery-rid";
        Environment.SetEnvironmentVariable("PATH", string.Empty);

        Assert.Throws<FileNotFoundException>(() => _manager.GetTailwindPath());
    }

    [Fact]
    public void GetTailwindPath_ThrowsFileNotFoundException_IfNotFoundAnywhere()
    {
        Environment.SetEnvironmentVariable("PATH", string.Empty);

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => _manager.GetTailwindPath());
    }

    [Fact]
    public void GetTailwindPath_LogsDebug_WhenPathEnvironmentVariableIsUnavailable()
    {
        Environment.SetEnvironmentVariable("PATH", null);

        Assert.Throws<FileNotFoundException>(() => _manager.GetTailwindPath());

        A.CallTo(_logger)
            .Where(call => call.Method.Name == "Log"
                && call.Arguments.Count > 2
                && Equals(call.Arguments[0], LogLevel.Debug)
                && call.Arguments[2] != null
                && call.Arguments[2]!.ToString()!.Contains("PATH environment variable is not set.", StringComparison.Ordinal))
            .MustHaveHappened();
    }

    [Theory]
    [MemberData(nameof(RidMatrixCases))]
    public void ResolveRid_ReturnsExpectedMapping(OSPlatform osPlatform, Architecture architecture, string expectedRid)
    {
        var result = TailwindCliManager.ResolveRid(osPlatform, architecture);

        Assert.Equal(expectedRid, result);
    }

    [Theory]
    [MemberData(nameof(CurrentRidOverrideCases))]
    public void GetCurrentRid_UsesCurrentPlatformOverrides(
        Func<OSPlatform, bool> osPlatformOverride,
        Architecture architecture,
        string expectedRid)
    {
        TailwindCliManager.IsOSPlatformOverride = osPlatformOverride;
        TailwindCliManager.ProcessArchitectureOverride = () => architecture;

        var result = TailwindCliManager.GetCurrentRid();

        Assert.Equal(expectedRid, result);
    }

    public static IEnumerable<object[]> RidMatrixCases()
    {
        yield return [OSPlatform.Windows, Architecture.X64, "win-x64"];
        yield return [OSPlatform.Windows, Architecture.Arm64, "win-x64"];
        yield return [OSPlatform.Windows, Architecture.X86, "unknown"];
        yield return [OSPlatform.Linux, Architecture.X64, "linux-x64"];
        yield return [OSPlatform.Linux, Architecture.Arm64, "linux-arm64"];
        yield return [OSPlatform.Linux, Architecture.X86, "unknown"];
        yield return [OSPlatform.OSX, Architecture.X64, "osx-x64"];
        yield return [OSPlatform.OSX, Architecture.Arm64, "osx-arm64"];
        yield return [OSPlatform.OSX, Architecture.X86, "unknown"];
        yield return [OSPlatform.Create("FREEBSD"), Architecture.X64, "unknown"];
    }

    public static IEnumerable<object[]> DevelopmentRuntimeRidCases()
    {
        yield return ["win-x64", "tailwindcss-windows-x64.exe"];
        yield return ["osx-arm64", "tailwindcss-macos-arm64"];
        yield return ["osx-x64", "tailwindcss-macos-x64"];
        yield return ["linux-arm64", "tailwindcss-linux-arm64"];
        yield return ["linux-x64", "tailwindcss-linux-x64"];
    }

    public static IEnumerable<object[]> CurrentRidOverrideCases()
    {
        yield return [(Func<OSPlatform, bool>)(platform => platform == OSPlatform.Windows), Architecture.X64, "win-x64"];
        yield return [(Func<OSPlatform, bool>)(platform => platform == OSPlatform.Linux), Architecture.Arm64, "linux-arm64"];
        yield return [(Func<OSPlatform, bool>)(platform => platform == OSPlatform.OSX), Architecture.X64, "osx-x64"];
        yield return [(Func<OSPlatform, bool>)(_ => false), Architecture.X64, "unknown"];
    }

    private static string GetRuntimeProjectBinaryName(string rid) => rid switch
    {
        "win-x64" => "tailwindcss-windows-x64.exe",
        "osx-arm64" => "tailwindcss-macos-arm64",
        "osx-x64" => "tailwindcss-macos-x64",
        "linux-arm64" => "tailwindcss-linux-arm64",
        "linux-x64" => "tailwindcss-linux-x64",
        _ => throw new InvalidOperationException($"Unsupported RID for test: {rid}")
    };
}
