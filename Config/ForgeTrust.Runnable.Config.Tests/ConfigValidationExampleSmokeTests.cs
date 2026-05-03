using System.Diagnostics;

namespace ForgeTrust.Runnable.Config.Tests;

public sealed class ConfigValidationExampleSmokeTests
{
    [Fact]
    public async Task ConfigValidationExample_FailsWithScalarValidationMessage()
    {
        var repositoryRoot = FindRepositoryRoot();
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "run --project examples/config-validation -p:NodeReuse=false",
            WorkingDirectory = repositoryRoot.FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        process.StartInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        process.StartInfo.Environment["DOTNET_NOLOGO"] = "1";

        process.Start();

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        var waitForExit = process.WaitForExitAsync();
        var completed = await Task.WhenAny(waitForExit, Task.Delay(TimeSpan.FromSeconds(30)));

        if (completed != waitForExit)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("The config validation example did not finish within 30 seconds.");
        }

        var outputTask = Task.WhenAll(stdout, stderr);
        var outputCompleted = await Task.WhenAny(outputTask, Task.Delay(TimeSpan.FromSeconds(5)));
        if (outputCompleted != outputTask)
        {
            throw new TimeoutException("The config validation example output streams did not close within 5 seconds.");
        }

        var output = string.Concat(await stdout, await stderr);

        Assert.NotEqual(0, process.ExitCode);
        Assert.Contains("Configuration validation failed for key 'PortConfig'", output);
        Assert.Contains("- <value>: The configuration value must be between 1 and 65535.", output);
        Assert.Contains("Fix the configured value or relax the scalar rule", output);
        Assert.DoesNotContain("70000", output);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ForgeTrust.Runnable.slnx")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the repository root.");
    }
}
