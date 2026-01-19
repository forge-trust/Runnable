using Xunit;

namespace ForgeTrust.Runnable.Core.Tests;

public class StringUtilsTests
{
    [Theory]
    [InlineData("MyMethod", "MyMethod")]
    [InlineData("My Method", "My-Method")]
    [InlineData("Method(int, string)", "Method-int--string")]
    [InlineData("  Trim  ", "Trim")]
    [InlineData("Invalid!@#Char", "Invalid---Char")]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void ToSafeIdentifier_ShouldSanitizeGenericInputs(string? input, string expected)
    {
        var result = StringUtils.ToSafeIdentifier(input);
        Assert.Equal(expected, result);
    }
}
