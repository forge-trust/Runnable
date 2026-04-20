using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ForgeTrust.Runnable.Web.Tailwind.Tests;

public sealed class TailwindBuildTargetsTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        $"{nameof(TailwindBuildTargetsTests)}_{Guid.NewGuid():N}");

    public TailwindBuildTargetsTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task RunTailwindBuild_Fails_WhenInputAndOutputResolveToSameFile()
    {
        var projectDirectory = Path.Combine(_tempRoot, "sample-app");
        Directory.CreateDirectory(Path.Combine(projectDirectory, "wwwroot", "css"));
        Directory.CreateDirectory(Path.Combine(projectDirectory, "tools"));

        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "wwwroot", "css", "app.css"),
            "@import \"tailwindcss\";" + Environment.NewLine);

        var markerPath = Path.Combine(projectDirectory, "tailwind-cli-executed.marker");
        var cliRelativePath = await CreateTailwindCliStubAsync(projectDirectory, markerPath);
        var projectPath = Path.Combine(projectDirectory, "Sample.csproj");
        var targetsPath = GetTailwindTargetsPath();

        await File.WriteAllTextAsync(
            projectPath,
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <TailwindInputPath>wwwroot/css/app.css</TailwindInputPath>
                <TailwindOutputPath>./wwwroot/css/../css/app.css</TailwindOutputPath>
                <TailwindCliPath>{{cliRelativePath}}</TailwindCliPath>
              </PropertyGroup>

              <Import Project="{{EscapeForXml(targetsPath)}}" />
            </Project>
            """);

        var result = await RunDotNetBuildAsync(projectPath, projectDirectory);
        var combinedOutput = result.Stdout + Environment.NewLine + result.Stderr;

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            "TailwindInputPath and TailwindOutputPath must point to different files.",
            combinedOutput,
            StringComparison.Ordinal);
        Assert.False(File.Exists(markerPath));
    }

    private static string GetTailwindTargetsPath()
    {
        return Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "Web",
                "ForgeTrust.Runnable.Web.Tailwind",
                "build",
                "ForgeTrust.Runnable.Web.Tailwind.targets"));
    }

    private static string EscapeForXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static async Task<string> CreateTailwindCliStubAsync(string projectDirectory, string markerPath)
    {
        var toolsDirectory = Path.Combine(projectDirectory, "tools");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var relativePath = Path.Combine("tools", "tailwindcss.cmd");
            var fullPath = Path.Combine(projectDirectory, relativePath);
            await File.WriteAllTextAsync(
                fullPath,
                $"""
                @echo off
                echo invoked>"{markerPath}"
                exit /b 0
                """);

            return relativePath;
        }

        const UnixFileMode executableMode =
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

        var unixRelativePath = Path.Combine("tools", "tailwindcss");
        var unixFullPath = Path.Combine(projectDirectory, unixRelativePath);
        await File.WriteAllTextAsync(
            unixFullPath,
            $"""
            #!/bin/sh
            printf 'invoked\n' > "{markerPath}"
            exit 0
            """);
        File.SetUnixFileMode(unixFullPath, executableMode);
        return unixRelativePath;
    }

    private static async Task<DotNetCommandResult> RunDotNetBuildAsync(string projectPath, string workingDirectory)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.StartInfo.ArgumentList.Add("build");
        process.StartInfo.ArgumentList.Add(projectPath);
        process.StartInfo.ArgumentList.Add("-nologo");
        process.StartInfo.ArgumentList.Add("-v:minimal");

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        return new DotNetCommandResult(
            process.ExitCode,
            await stdoutTask,
            await stderrTask);
    }

    private sealed record DotNetCommandResult(int ExitCode, string Stdout, string Stderr);
}
