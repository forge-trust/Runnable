namespace ForgeTrust.Runnable.PackageIndex;

/// <summary>
/// CLI entry point for generating or verifying the package chooser.
/// </summary>
internal static class Program
{
    private const string GenerateCommand = "generate";
    private const string VerifyCommand = "verify";

    private static readonly string Usage = """
        ForgeTrust.Runnable.PackageIndex

        Generates and verifies the public Runnable package chooser.

        Usage:
          dotnet run --project tools/ForgeTrust.Runnable.PackageIndex/ForgeTrust.Runnable.PackageIndex.csproj -- <command> [options]

        Commands:
          generate    Rewrite packages/README.md from packages/package-index.yml and project metadata.
          verify      Check that packages/README.md is already up to date.

        Options:
          --repo-root <path>    Repository root. Defaults to the current directory.
          --manifest <path>     Package manifest path. Defaults to packages/package-index.yml.
          --output <path>       Generated chooser path. Defaults to packages/README.md.
          -h, --help            Show this help.
        """;

    /// <summary>
    /// Launches the package chooser CLI with the current process IO streams and working directory.
    /// </summary>
    /// <param name="args">Command-line arguments supplied to the process.</param>
    /// <returns>Process exit code where <c>0</c> indicates success.</returns>
    internal static async Task<int> Main(string[] args)
    {
        return await RunAsync(args, Console.Out, Console.Error, Directory.GetCurrentDirectory());
    }

    /// <summary>
    /// Runs the package chooser CLI against the supplied IO streams and working directory.
    /// </summary>
    /// <param name="args">Command-line arguments, including the command and optional path overrides.</param>
    /// <param name="standardOut">Writer that receives success messages.</param>
    /// <param name="standardError">Writer that receives usage and failure messages.</param>
    /// <param name="currentDirectory">Working directory used to resolve default repository-relative paths.</param>
    /// <param name="cancellationToken">Cancellation token propagated to generator operations.</param>
    /// <returns><c>0</c> when the command succeeds; otherwise a non-zero exit code.</returns>
    internal static async Task<int> RunAsync(
        string[] args,
        TextWriter standardOut,
        TextWriter standardError,
        string currentDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(standardOut);
        ArgumentNullException.ThrowIfNull(standardError);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);

        if (args.Length == 0)
        {
            await standardError.WriteLineAsync(Usage);
            return 1;
        }

        if (IsHelp(args[0]))
        {
            await standardOut.WriteLineAsync(Usage);
            return 0;
        }

        try
        {
            var command = args[0].Trim();
            if (args.Skip(1).Any(IsHelp))
            {
                await standardOut.WriteLineAsync(Usage);
                return 0;
            }

            var normalizedCommand = command.ToLowerInvariant();
            if (normalizedCommand is not GenerateCommand and not VerifyCommand)
            {
                await standardError.WriteLineAsync($"Unknown command '{command}'.");
                await standardError.WriteLineAsync(Usage);
                return 1;
            }

            var options = CommandLineOptions.Parse(args.Skip(1).ToArray(), currentDirectory);
            var generator = new PackageIndexGenerator(
                new PackageProjectScanner(),
                new DotNetProjectMetadataProvider(),
                new PackageManifestLoader());

            if (normalizedCommand == GenerateCommand)
            {
                await generator.GenerateToFileAsync(options.Request, cancellationToken);
                var outputPath = Path.GetRelativePath(options.Request.RepositoryRoot, options.Request.OutputPath)
                    .Replace('\\', '/');
                await standardOut.WriteLineAsync($"Generated {outputPath}.");
                return 0;
            }

            await generator.VerifyAsync(options.Request, cancellationToken);
            await standardOut.WriteLineAsync("Package chooser is up to date.");
            return 0;
        }
        catch (PackageIndexException ex)
        {
            await standardError.WriteLineAsync(ex.Message);
            return 1;
        }
    }

    private static bool IsHelp(string argument)
    {
        return string.Equals(argument, "--help", StringComparison.Ordinal)
            || string.Equals(argument, "-h", StringComparison.Ordinal);
    }
}

/// <summary>
/// Parsed CLI options for one package chooser command invocation.
/// </summary>
/// <param name="Request">Resolved package chooser request derived from command-line options.</param>
internal sealed record CommandLineOptions(PackageIndexRequest Request)
{
    /// <summary>
    /// Parses path-related CLI options into a resolved chooser request.
    /// </summary>
    /// <param name="args">Arguments after the command verb.</param>
    /// <param name="currentDirectory">Working directory used to resolve relative overrides.</param>
    /// <returns>The parsed command-line options.</returns>
    /// <exception cref="PackageIndexException">Thrown when an option is unknown or missing its required value.</exception>
    internal static CommandLineOptions Parse(string[] args, string currentDirectory)
    {
        string? repositoryRoot = null;
        string? manifestPath = null;
        string? outputPath = null;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--repo-root", StringComparison.Ordinal))
            {
                repositoryRoot = ReadRequiredValue(args, ref index, argument);
                continue;
            }

            if (string.Equals(argument, "--manifest", StringComparison.Ordinal))
            {
                manifestPath = ReadRequiredValue(args, ref index, argument);
                continue;
            }

            if (string.Equals(argument, "--output", StringComparison.Ordinal))
            {
                outputPath = ReadRequiredValue(args, ref index, argument);
                continue;
            }

            throw new PackageIndexException($"Unknown option '{argument}'.");
        }

        var repoRoot = ResolvePath(repositoryRoot, currentDirectory, currentDirectory);
        var resolvedManifestPath = ResolvePath(manifestPath, repoRoot, Path.Combine(repoRoot, "packages", "package-index.yml"));
        var resolvedOutputPath = ResolvePath(outputPath, repoRoot, Path.Combine(repoRoot, "packages", "README.md"));

        return new CommandLineOptions(new PackageIndexRequest(repoRoot, resolvedManifestPath, resolvedOutputPath));
    }

    private static string ReadRequiredValue(string[] args, ref int index, string argument)
    {
        if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            throw new PackageIndexException($"Option '{argument}' requires a value.");
        }

        index++;
        return args[index];
    }

    private static string ResolvePath(string? value, string baseDirectory, string defaultPath)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Path.GetFullPath(defaultPath);
        }

        return Path.IsPathRooted(value)
            ? Path.GetFullPath(value)
            : Path.GetFullPath(Path.Combine(baseDirectory, value));
    }
}
