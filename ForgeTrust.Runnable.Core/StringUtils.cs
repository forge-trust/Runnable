using System.Text.RegularExpressions;

namespace ForgeTrust.Runnable.Core;

/// <summary>
/// Provides shared string manipulation utilities.
/// </summary>
public static partial class StringUtils
{
    [GeneratedRegex("[^a-zA-Z0-9_-]")]
    private static partial Regex IdentifierRegex();

    /// <summary>
    /// Sanitizes an input string for use as a safe identifier or URL anchor.
    /// Replaces non-alphanumeric characters with hyphens and trims leading/trailing hyphens.
    /// </summary>
    /// <param name="input">The input string to sanitize.</param>
    /// <returns>A sanitized safe identifier.</returns>
    public static string ToSafeIdentifier(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        // Replace non-alphanumeric chars with hyphens
        return IdentifierRegex().Replace(input, "-").Trim('-');
    }
}
