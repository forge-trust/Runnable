using ForgeTrust.Runnable.Web;

namespace ForgeTrust.Runnable.Web.Tests;

public sealed class RunnableWebDevelopmentPortDefaultsTests
{
    [Fact]
    public void Resolve_AppendsDeterministicPort_WhenNoExplicitUrlConfigurationExists()
    {
        using var environment = new TemporaryEnvironment();
        environment.CreateGitRepo("workspace");
        var appBaseDirectory = environment.CreateApplicationBaseDirectory("workspace");

        var resolution = RunnableWebDevelopmentPortDefaults.Resolve(
            [],
            environment.WorkspaceRoot,
            appBaseDirectory,
            _ => null);

        Assert.NotNull(resolution.AppliedPort);
        Assert.Equal(environment.WorkspaceRoot, resolution.SeedPath);
        Assert.Equal(["--port", resolution.AppliedPort.Value.ToString()], resolution.Args);
        Assert.InRange(resolution.AppliedPort.Value, 5600, 6599);
    }

    [Fact]
    public void Resolve_UsesSamePort_ForDifferentDirectoriesInsideTheSameRepository()
    {
        using var environment = new TemporaryEnvironment();
        environment.CreateGitRepo("workspace");
        var appBaseDirectory = environment.CreateApplicationBaseDirectory("workspace");
        var nestedDirectory = Path.Combine(environment.WorkspaceRoot, "Web", "Docs");
        Directory.CreateDirectory(nestedDirectory);

        var rootResolution = RunnableWebDevelopmentPortDefaults.Resolve(
            [],
            environment.WorkspaceRoot,
            appBaseDirectory,
            _ => null);
        var nestedResolution = RunnableWebDevelopmentPortDefaults.Resolve(
            [],
            nestedDirectory,
            appBaseDirectory,
            _ => null);

        Assert.Equal(rootResolution.AppliedPort, nestedResolution.AppliedPort);
        Assert.Equal(rootResolution.SeedPath, nestedResolution.SeedPath);
    }

    [Fact]
    public void Resolve_UsesDifferentPorts_ForDifferentRepositoryRoots()
    {
        using var first = new TemporaryEnvironment();
        using var second = new TemporaryEnvironment();
        first.CreateGitRepo("saturn");
        second.CreateGitRepo("jupiter");

        var firstResolution = RunnableWebDevelopmentPortDefaults.Resolve(
            [],
            first.WorkspaceRoot,
            first.CreateApplicationBaseDirectory("saturn"),
            _ => null);
        var secondResolution = RunnableWebDevelopmentPortDefaults.Resolve(
            [],
            second.WorkspaceRoot,
            second.CreateApplicationBaseDirectory("jupiter"),
            _ => null);

        Assert.NotEqual(firstResolution.AppliedPort, secondResolution.AppliedPort);
    }

    [Fact]
    public void Resolve_DoesNotOverrideExplicitPortArgument()
    {
        using var environment = new TemporaryEnvironment();
        environment.CreateGitRepo("workspace");
        var args = new[] { "--port", "5005" };

        var resolution = RunnableWebDevelopmentPortDefaults.Resolve(
            args,
            environment.WorkspaceRoot,
            environment.CreateApplicationBaseDirectory("workspace"),
            _ => null);

        Assert.Null(resolution.AppliedPort);
        Assert.Same(args, resolution.Args);
    }

    [Fact]
    public void Resolve_DoesNotOverrideExplicitUrlsArgument()
    {
        using var environment = new TemporaryEnvironment();
        environment.CreateGitRepo("workspace");
        var args = new[] { "--urls", "http://127.0.0.1:5005" };

        var resolution = RunnableWebDevelopmentPortDefaults.Resolve(
            args,
            environment.WorkspaceRoot,
            environment.CreateApplicationBaseDirectory("workspace"),
            _ => null);

        Assert.Null(resolution.AppliedPort);
        Assert.Same(args, resolution.Args);
    }

    [Fact]
    public void Resolve_DoesNotOverrideAspNetCoreUrlsEnvironmentVariable()
    {
        using var environment = new TemporaryEnvironment();
        environment.CreateGitRepo("workspace");

        var resolution = RunnableWebDevelopmentPortDefaults.Resolve(
            [],
            environment.WorkspaceRoot,
            environment.CreateApplicationBaseDirectory("workspace"),
            key => key == "ASPNETCORE_URLS" ? "http://127.0.0.1:5005" : null);

        Assert.Null(resolution.AppliedPort);
        Assert.Empty(resolution.Args);
    }

    [Fact]
    public void Resolve_FallsBackToProjectRoot_WhenNoRepositoryRootExists()
    {
        using var environment = new TemporaryEnvironment();
        var appBaseDirectory = environment.CreateApplicationBaseDirectory("standalone");
        var workingDirectory = Path.Combine(environment.RootDirectory, "work");
        Directory.CreateDirectory(workingDirectory);

        var resolution = RunnableWebDevelopmentPortDefaults.Resolve(
            [],
            workingDirectory,
            appBaseDirectory,
            _ => null);

        Assert.Equal(environment.ProjectRoot, resolution.SeedPath);
        Assert.NotNull(resolution.AppliedPort);
    }

    private sealed class TemporaryEnvironment : IDisposable
    {
        public TemporaryEnvironment()
        {
            RootDirectory = Path.Combine(
                Path.GetTempPath(),
                "runnable-web-port-defaults-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootDirectory);
        }

        public string RootDirectory { get; }

        public string WorkspaceRoot { get; private set; } = string.Empty;

        public string ProjectRoot { get; private set; } = string.Empty;

        public void CreateGitRepo(string workspaceName)
        {
            WorkspaceRoot = Path.Combine(RootDirectory, workspaceName);
            Directory.CreateDirectory(WorkspaceRoot);
            File.WriteAllText(Path.Combine(WorkspaceRoot, ".git"), "gitdir: test");
        }

        public string CreateApplicationBaseDirectory(string projectName)
        {
            var containerRoot = string.IsNullOrEmpty(WorkspaceRoot) ? RootDirectory : WorkspaceRoot;
            ProjectRoot = Path.Combine(containerRoot, projectName);
            Directory.CreateDirectory(ProjectRoot);
            File.WriteAllText(Path.Combine(ProjectRoot, $"{projectName}.csproj"), "<Project />");

            var baseDirectory = Path.Combine(ProjectRoot, "bin", "Debug", "net10.0");
            Directory.CreateDirectory(baseDirectory);
            return baseDirectory;
        }

        public void Dispose()
        {
            if (Directory.Exists(RootDirectory))
            {
                Directory.Delete(RootDirectory, recursive: true);
            }
        }
    }
}
