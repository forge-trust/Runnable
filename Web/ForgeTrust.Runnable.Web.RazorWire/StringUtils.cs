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
    /// <summary>
    /// Produces a safe identifier by replacing disallowed characters with hyphens, collapsing consecutive hyphens, trimming edge hyphens, and defaulting to "id" for null, whitespace, or empty results.
    /// </summary>
    /// <param name="input">The source string to convert into a safe identifier.</param>
    /// <param name="appendHash">If true, appends a short deterministic 4-character lowercase hex hash (prefixed with a hyphen) derived from the original input.</param>
    /// <returns>The sanitized identifier; when <paramref name="appendHash"/> is true the result is suffixed with "-" followed by a 4-character hash.</returns>
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
    /// Generates a short, deterministic 4-character hex (16 bits) hash of the input string using SHA256.
    /// </summary>
    /// <remarks>
    /// This method <c>GetDeterministicHash</c> returns a 4-character lowercase hex string. 
    /// Due to the short length (16 bits), the collision likelihood is subject to the birthday paradox: 
    /// there is ~50% chance of collision with only ~256 unique inputs.
    /// It is NOT guaranteed to be unique for large sets. If higher uniqueness is required, 
    /// consider returning a longer slice of the hash or the full SHA256 string.
    /// </remarks>
    /// <param name="input">The string to hash.</param>
    /// <summary>
    /// Produces a short deterministic 4-character lowercase hexadecimal hash derived from <paramref name="input"/> using SHA-256.
    /// </summary>
    /// <param name="input">The input string to hash.</param>
    /// <returns>A 4-character lowercase hex string.</returns>
    /// <remarks>
    /// Returns only the first 4 hex characters (16 bits) of the SHA-256 digest; collisions are possible.
    /// </remarks>
    private static string GetDeterministicHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);

        return Convert.ToHexString(hashBytes)[..4].ToLowerInvariant();
    }
}