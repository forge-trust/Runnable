namespace ForgeTrust.Runnable.PackageIndex;

internal static class Program
{
    private const string GenerateCommand = "generate";
    private const string VerifyCommand = "verify";

    private static readonly string Usage = """
        Usage:
          dotnet run --project tools/ForgeTrust.Runnable.PackageIndex/ForgeTrust.Runnable.PackageIndex.csproj -- generate [--repo-root <path>] [--manifest <path>] [--output <path>]
          dotnet run --project tools/ForgeTrust.Runnable.PackageIndex/ForgeTrust.Runnable.PackageIndex.csproj -- verify [--repo-root <path>] [--manifest <path>] [--output <path>]
        """;

    internal static async Task<int> Main(string[] args)
    {
        return await RunAsync(args, Console.Out, Console.Error, Directory.GetCurrentDirectory());
    }

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

        try
        {
            var command = args[0].Trim();
            var options = CommandLineOptions.Parse(args.Skip(1).ToArray(), currentDirectory);
            var generator = new PackageIndexGenerator(
                new PackageProjectScanner(),
                new DotNetProjectMetadataProvider(),
                new PackageManifestLoader());

            switch (command.ToLowerInvariant())
            {
                case GenerateCommand:
                    {
                        await generator.GenerateToFileAsync(options.Request, cancellationToken);
                        await standardOut.WriteLineAsync($"Generated {Path.GetRelativePath(options.Request.RepositoryRoot, options.Request.OutputPath)}.");
                        return 0;
                    }

                case VerifyCommand:
                    {
                        await generator.VerifyAsync(options.Request, cancellationToken);
                        await standardOut.WriteLineAsync("Package chooser is up to date.");
                        return 0;
                    }

                default:
                    await standardError.WriteLineAsync(Usage);
                    return 1;
            }
        }
        catch (PackageIndexException ex)
        {
            await standardError.WriteLineAsync(ex.Message);
            return 1;
        }
    }
}

internal sealed record CommandLineOptions(PackageIndexRequest Request)
{
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
