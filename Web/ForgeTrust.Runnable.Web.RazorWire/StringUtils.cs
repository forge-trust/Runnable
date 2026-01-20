using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ForgeTrust.Runnable.Web.RazorWire;

/// <summary>
/// Provides utility methods for string manipulation, specifically for generating safe identifiers.
/// </summary>
public static class StringUtils
{
    private static readonly Regex IdentifierRegex = new(@"[^a-zA-Z0-9-_]", RegexOptions.Compiled);
    private static readonly Regex MultiHyphenRegex = new(@"-+", RegexOptions.Compiled);

    /// <summary>
    /// Produces a safe identifier from an input string by replacing characters not in [a-zA-Z0-9-_] with hyphens.
    /// Optionally appends a short deterministic hash of the original input to ensure uniqueness.
    /// </summary>
    /// <param name="input">The original string to normalize.</param>
    /// <param name="appendHash">If true, appends a 4-character deterministic hash suffix.</param>
    /// <returns>The sanitized identifier.</returns>
    public static string ToSafeId(string? input, bool appendHash = false)
    {
        string sanitized;
        if (string.IsNullOrWhiteSpace(input))
        {
            sanitized = "id";
        }
        else
        {
            // 1. Sanitize: Replace non-alphanumeric (except - and _) with -
            sanitized = IdentifierRegex.Replace(input, "-");

            // 2. Clean up: Trim and normalize hyphens using Regex
            sanitized = MultiHyphenRegex.Replace(sanitized, "-").Trim('-');

            if (string.IsNullOrEmpty(sanitized))
            {
                sanitized = "id";
            }
        }

        if (!appendHash)
        {
            return sanitized;
        }

        // 3. Optional: Append a short deterministic hash of the original input
        var hash = GetDeterministicHash(input ?? string.Empty);

        return $"{sanitized}-{hash}";
    }

    /// <summary>
    /// Generates a short, deterministic 4-character hex hash of the input string.
    /// </summary>
    /// <param name="input">The string to hash.</param>
    /// <returns>A 4-character lowercase hex string.</returns>
    private static string GetDeterministicHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);

        return Convert.ToHexString(hashBytes)[..4].ToLowerInvariant();
    }
}
