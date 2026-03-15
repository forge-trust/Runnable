using CliFx.Exceptions;
using CliFx.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli.Tests;

public class TailwindBuildCommandTests
{
    [Fact]
    public async Task ExecuteAsync_Should_Run_Tailwind_Build_With_Resolved_Paths()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var projectPath = Path.Combine(tempDir, "Example.csproj");
        var inputPath = Path.Combine(tempDir, "tailwind.css");
        await File.WriteAllTextAsync(projectPath, "<Project />");
        await File.WriteAllTextAsync(inputPath, "@import \"tailwindcss\";");

        try
        {
            var resolver = new StubTailwindExecutableResolver("/tailwindcss");
            var runner = new FakeToolProcessRunner { ExitCode = 0 };

            var command = new TailwindBuildCommand(
                resolver,
                runner,
                NullLogger<TailwindBuildCommand>.Instance)
            {
                ProjectPath = projectPath,
                InputPath = "tailwind.css",
                OutputPath = "wwwroot/css/site.css"
            };
            await command.ExecuteAsync(new FakeInMemoryConsole(), CancellationToken.None);

            Assert.Equal("/tailwindcss", runner.Spec?.FileName);
            Assert.Equal(tempDir, runner.Spec?.WorkingDirectory);
            Assert.Equal(
                ["-i", inputPath, "-o", Path.Combine(tempDir, "wwwroot", "css", "site.css")],
                runner.Spec?.Arguments);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_Should_Throw_When_Build_Fails()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        var projectPath = Path.Combine(tempDir, "Example.csproj");
        await File.WriteAllTextAsync(projectPath, "<Project />");
        await File.WriteAllTextAsync(Path.Combine(tempDir, "tailwind.css"), "@import \"tailwindcss\";");

        try
        {
            var command = new TailwindBuildCommand(
                new StubTailwindExecutableResolver("/tailwindcss"),
                new FakeToolProcessRunner { ExitCode = 1 },
                NullLogger<TailwindBuildCommand>.Instance)
            {
                ProjectPath = projectPath
            };

            var ex = await Assert.ThrowsAsync<CommandException>(
                async () => await command.ExecuteAsync(new FakeInMemoryConsole(), CancellationToken.None));
            Assert.Contains("exit code 1", ex.Message);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private sealed class FakeToolProcessRunner : IToolProcessRunner
    {
        public ProcessLaunchSpec? Spec { get; private set; }

        public int ExitCode { get; init; }

        public Task<int> RunAsync(
            ProcessLaunchSpec spec,
            Action<string>? onOutput,
            Action<string>? onError,
            CancellationToken cancellationToken)
        {
            Spec = spec;
            return Task.FromResult(ExitCode);
        }
    }

    private sealed class StubTailwindExecutableResolver : ITailwindExecutableResolver
    {
        private readonly string _executablePath;

        public StubTailwindExecutableResolver(string executablePath)
        {
            _executablePath = executablePath;
        }

        public Task<string> ResolveAsync(TailwindExecutableRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_executablePath);
        }
    }
}
