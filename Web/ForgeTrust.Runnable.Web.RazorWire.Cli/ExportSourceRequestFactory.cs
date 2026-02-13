using CliFx.Exceptions;
using System.Diagnostics.CodeAnalysis;

namespace ForgeTrust.Runnable.Web.RazorWire.Cli;

/// <summary>
/// Creates validated export source requests from CLI options.
/// </summary>
[ExcludeFromCodeCoverage]
public class ExportSourceRequestFactory
{
    internal ExportSourceRequest Create(
        string? baseUrl,
        string? projectPath,
        string? dllPath,
        IReadOnlyList<string> appArgs)
    {
        var sources = new[]
        {
            !string.IsNullOrWhiteSpace(baseUrl),
            !string.IsNullOrWhiteSpace(projectPath),
            !string.IsNullOrWhiteSpace(dllPath)
        };

        var selectedCount = sources.Count(selected => selected);
        if (selectedCount == 0)
        {
            throw new CommandException("You must specify exactly one source: --url, --project, or --dll.");
        }

        if (selectedCount > 1)
        {
            throw new CommandException("Source options are mutually exclusive. Specify only one of --url, --project, or --dll.");
        }

        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new CommandException("BaseUrl must be a valid HTTP or HTTPS URL.");
            }

            return new ExportSourceRequest(
                ExportSourceKind.Url,
                uri.ToString().TrimEnd('/'),
                appArgs);
        }

        if (!string.IsNullOrWhiteSpace(projectPath))
        {
            var fullPath = ValidateFile(projectPath, ".csproj", "--project");
            return new ExportSourceRequest(ExportSourceKind.Project, fullPath, appArgs);
        }

        var dllFullPath = ValidateFile(dllPath!, ".dll", "--dll");
        return new ExportSourceRequest(ExportSourceKind.Dll, dllFullPath, appArgs);
    }

    private static string ValidateFile(string filePath, string extension, string optionName)
    {
        if (!Path.HasExtension(filePath)
            || !string.Equals(Path.GetExtension(filePath), extension, StringComparison.OrdinalIgnoreCase))
        {
            throw new CommandException($"{optionName} must point to a {extension} file.");
        }

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            throw new CommandException($"{optionName} file not found: {fullPath}");
        }

        return fullPath;
    }
}
