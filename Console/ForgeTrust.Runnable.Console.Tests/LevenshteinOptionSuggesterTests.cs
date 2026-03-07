namespace ForgeTrust.Runnable.Console.Tests;

public class LevenshteinOptionSuggesterTests
{
    private readonly LevenshteinOptionSuggester _suggester = new();

    [Fact]
    public void GetSuggestions_WithKnownTypos_ReturnsClosestMatch()
    {
        var validOptions = new[] { "--help", "--version", "--config", "--force" };
        var suggestions = _suggester.GetSuggestions("--hepl", validOptions).ToList();

        Assert.Single(suggestions);
        Assert.Equal("--help", suggestions[0]);
    }

    [Fact]
    public void GetSuggestions_WhenNoCloseMatch_ReturnsEmpty()
    {
        var validOptions = new[] { "--help", "--version" };
        var suggestions = _suggester.GetSuggestions("--something-completely-different", validOptions).ToList();

        Assert.Empty(suggestions);
    }

    [Fact]
    public void GetSuggestions_WithEmptyInput_ReturnsEmpty()
    {
        var validOptions = new[] { "--help" };

        Assert.Empty(_suggester.GetSuggestions(string.Empty, validOptions));
        Assert.Empty(_suggester.GetSuggestions(null!, validOptions));
    }

    [Fact]
    public void GetSuggestions_WithExactMatch_ReturnsExactMatch()
    {
        var validOptions = new[] { "--help", "--version" };
        var suggestions = _suggester.GetSuggestions("--help", validOptions).ToList();

        Assert.Single(suggestions);
        Assert.Equal("--help", suggestions[0]);
    }

    [Fact]
    public void GetSuggestions_IsCaseInsensitive()
    {
        var validOptions = new[] { "--help", "--Version" };
        var suggestions = _suggester.GetSuggestions("--HEPL", validOptions).ToList();

        Assert.Single(suggestions);
        Assert.Equal("--help", suggestions[0]);
    }
}
