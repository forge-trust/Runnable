namespace ForgeTrust.Runnable.Web.RazorWire.IntegrationTests;

[Trait("Category", "Unit")]
public sealed class DotnetLaunchResolverTests : IDisposable
{
    private readonly string _tempDirectory;

    public DotnetLaunchResolverTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "dotnet-launch-resolver-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void ResolveProjectLaunchArguments_ShouldPreferBuiltAssembly_WhenPreferredConfigurationExists()
    {
        var projectDirectory = Path.Combine(_tempDirectory, "Standalone");
        Directory.CreateDirectory(projectDirectory);
        var projectPath = Path.Combine(projectDirectory, "Standalone.csproj");
        File.WriteAllText(projectPath, "<Project />");

        var builtAssemblyPath = Path.Combine(projectDirectory, "bin", "Release", "net10.0", "Standalone.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(builtAssemblyPath)!);
        File.WriteAllText(builtAssemblyPath, string.Empty);

        var arguments = DotnetLaunchResolver.ResolveProjectLaunchArguments(projectPath, "Standalone", "Release");

        Assert.Equal($"\"{builtAssemblyPath}\"", arguments);
    }

    [Fact]
    public void ResolveProjectLaunchArguments_ShouldFallBackToDotnetRun_WhenBuiltAssemblyIsMissing()
    {
        var projectDirectory = Path.Combine(_tempDirectory, "Standalone");
        Directory.CreateDirectory(projectDirectory);
        var projectPath = Path.Combine(projectDirectory, "Standalone.csproj");
        File.WriteAllText(projectPath, "<Project />");

        var arguments = DotnetLaunchResolver.ResolveProjectLaunchArguments(projectPath, "Standalone", "Release");

        Assert.Equal($"run --project \"{projectPath}\" --no-launch-profile --configuration Release", arguments);
    }

    [Theory]
    [InlineData("/tmp/project/bin/Debug/net10.0/", "Debug")]
    [InlineData("/tmp/project/bin/Release/net10.0/", "Release")]
    [InlineData("/tmp/project/custom-output/", null)]
    public void TryGetCurrentBuildConfiguration_ShouldDetectKnownOutputConventions(string appBaseDirectory, string? expectedConfiguration)
    {
        var configuration = DotnetLaunchResolver.TryGetCurrentBuildConfiguration(appBaseDirectory);

        Assert.Equal(expectedConfiguration, configuration);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
