using System.Collections.Generic;

namespace ForgeTrust.Runnable.Console;

/// <summary>
/// Defines a contract for suggesting alternative options when an unknown option is provided.
/// </summary>
public interface IOptionSuggester
{
    /// <summary>
    /// Gets a list of suggested options based on the unknown option and the list of valid options.
    /// </summary>
    /// <param name="unknownOption">The unknown option provided by the user.</param>
    /// <param name="validOptions">The list of valid options for the current command.</param>
    /// <returns>A collection of suggested options.</returns>
    IEnumerable<string> GetSuggestions(string unknownOption, IEnumerable<string> validOptions);
}
