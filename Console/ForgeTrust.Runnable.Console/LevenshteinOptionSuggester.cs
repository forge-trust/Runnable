using System;
using System.Collections.Generic;
using System.Linq;

namespace ForgeTrust.Runnable.Console;

/// <summary>
///     Suggests options using the Levenshtein distance algorithm.
/// </summary>
public class LevenshteinOptionSuggester : IOptionSuggester
{
    private const int MaxDistance = 3;

    public IEnumerable<string> GetSuggestions(string unknownOption, IEnumerable<string> validOptions)
    {
        if (string.IsNullOrEmpty(unknownOption))
        {
            return Enumerable.Empty<string>();
        }

        // Clean the input option (remove leading dashes) for better matching if needed,
        // but typically we compare full strings or key names. 
        // Let's assume we are comparing the full flag token provided (e.g. "--foo") 
        // with valid flags (e.g. "--food").
        
        return validOptions
            .Select(option => new { Option = option, Distance = ComputeLevenshteinDistance(unknownOption, option) })
            .Where(x => x.Distance <= MaxDistance)
            .OrderBy(x => x.Distance)
            .Select(x => x.Option);
    }

    /// <summary>
    ///     Computes the Levenshtein distance between two strings.
    /// </summary>
    private static int ComputeLevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 0 : t.Length;
        if (string.IsNullOrEmpty(t)) return s.Length;

        var d = new int[s.Length + 1, t.Length + 1];

        for (var i = 0; i <= s.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= t.Length; j++) d[0, j] = j;

        for (var j = 1; j <= t.Length; j++)
        {
            for (var i = 1; i <= s.Length; i++)
            {
                var cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[s.Length, t.Length];
    }
}
