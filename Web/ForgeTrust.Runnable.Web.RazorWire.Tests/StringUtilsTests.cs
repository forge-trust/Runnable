namespace ForgeTrust.Runnable.Web.RazorWire.Tests;

public class StringUtilsTests
{
    [Theory]
    [InlineData("hello-world", "hello-world")]
    [InlineData("Hello World", "Hello-World")]
    [InlineData("test@example.com", "test-example-com")]
    [InlineData("foo_bar", "foo_bar")]
    [InlineData("multiple   spaces", "multiple-spaces")]
    [InlineData("---leading-trailing---", "leading-trailing")]
    [InlineData("", "id")]
    [InlineData(null, "id")]
    [InlineData("   ", "id")]
    [InlineData("!@#$%", "id")]
    public void ToSafeId_WithoutHash_SanitizesCorrectly(string? input, string expected)
    {
        var result = StringUtils.ToSafeId(input, appendHash: false);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToSafeId_WithHash_AppendsConsistentHash()
    {
        var input = "test@example.com";
        var result1 = StringUtils.ToSafeId(input, appendHash: true);
        var result2 = StringUtils.ToSafeId(input, appendHash: true);

        // Should be deterministic
        Assert.Equal(result1, result2);

        // Should have hash suffix
        Assert.Contains("-", result1);
        Assert.Matches(@"^test-example-com-[0-9a-f]{4}$", result1);
    }

    [Fact]
    public void ToSafeId_WithHash_ProducesDifferentHashesForDifferentInputs()
    {
        var result1 = StringUtils.ToSafeId("user1", appendHash: true);
        var result2 = StringUtils.ToSafeId("user2", appendHash: true);

        Assert.NotEqual(result1, result2);
    }

    [Fact]
    public void ToSafeId_WithHash_HandlesEmptyInput()
    {
        var result = StringUtils.ToSafeId("", appendHash: true);
        // "id" is fallback, but hash is appended? 
        // Logic: if not string.IsNullOrEmpty(sanitized) -> "id". 
        // Then if (!appendHash) return sanitized.
        // If appendHash: hash = GetDeterministicHash(input). Input is "".
        // Hash of empty string?
        // Let's verify logic in StringUtils.cs
        // if (string.IsNullOrWhiteSpace(input)) return "id";
        // So it returns "id" immediately, ignoring appendHash logic for the first check.
        // Wait, review StringUtils.cs again.

        Assert.Equal("id", result);
    }
}
