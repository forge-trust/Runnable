namespace ForgeTrust.Runnable.Web.RazorWire.Tests;

public class RazorWireOptionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DefaultFailureMessage_WhenAssignedNullOrBlank_UsesSafeFallback(string? value)
    {
        var options = new RazorWireOptions();

        options.Forms.DefaultFailureMessage = value!;

        Assert.Equal("We could not submit this form. Check your input and try again.", options.Forms.DefaultFailureMessage);
    }
}
